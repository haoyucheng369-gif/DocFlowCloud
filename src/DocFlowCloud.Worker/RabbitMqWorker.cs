using System.Text;
using System.Text.Json;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Abstractions.Processing;
using DocFlowCloud.Application.Exceptions;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Inbox;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace DocFlowCloud.Worker;

public sealed class RabbitMqWorker : BackgroundService
{
    private const string ConsumerName = "DocFlowCloud.JobConsumer";
    private const int MaxRetryCount = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqWorker> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqWorker(
        IServiceScopeFactory scopeFactory,
        RabbitMqSettings settings,
        ILogger<RabbitMqWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: _settings.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var mainQueueArguments = new Dictionary<string, object>
        {
            ["x-dead-letter-routing-key"] = _settings.DeadLetterQueueName
        };

        _channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments);

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _logger.LogInformation("RabbitMQ worker connected. Queue: {QueueName}", _settings.QueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            _logger.LogInformation("Received message: {Message}", json);

            try
            {
                var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json)
                    ?? throw new InvalidOperationException("Message deserialization failed.");

                using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();

                    var claimed = await inboxRepository.TryClaimAsync(
                        message.MessageId,
                        ConsumerName,
                        TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds),
                        stoppingToken);

                    if (!claimed)
                    {
                        _logger.LogWarning("Message was already claimed or processed. MessageId: {MessageId}", message.MessageId);
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        return;
                    }

                    var completion = await TryCompleteAlreadySucceededJobAsync(message, stoppingToken);
                    if (completion)
                    {
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        _logger.LogInformation("Job {JobId} was already succeeded. Side effects were skipped.", message.JobId);
                        return;
                    }

                    var resultJson = await ExecuteSideEffectsAsync(message, stoppingToken);
                    await CompleteMessageAsync(message, resultJson, stoppingToken);

                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    _logger.LogInformation("Job {JobId} processed successfully.", message.JobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing message.");

                await HandleFailureAsync(json, ex, body, eventArgs.BasicProperties, stoppingToken);
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("RabbitMQ consumer started.");

        return Task.CompletedTask;
    }

    private async Task<bool> TryCompleteAlreadySucceededJobAsync(
        JobCreatedIntegrationMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        if (job.Status != JobStatus.Succeeded)
        {
            return false;
        }

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
            x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
            cancellationToken)
            ?? throw new InvalidOperationException($"Inbox claim for message '{message.MessageId}' was not found.");

        if (inbox.Status == InboxStatus.Processing)
        {
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return true;
    }

    private async Task<string> ExecuteSideEffectsAsync(
        JobCreatedIntegrationMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var executor = scope.ServiceProvider.GetRequiredService<IJobSideEffectExecutor>();

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        return await executor.ExecuteAsync(
            job.Id,
            job.Type,
            job.PayloadJson,
            message.IdempotencyKey,
            message.CorrelationId,
            cancellationToken);
    }

    private async Task CompleteMessageAsync(
        JobCreatedIntegrationMessage message,
        string resultJson,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
            x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
            cancellationToken)
            ?? throw new InvalidOperationException($"Inbox claim for message '{message.MessageId}' was not found.");

        if (job.Status == JobStatus.Succeeded)
        {
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        job.MarkProcessing();
        job.MarkSucceeded(resultJson);
        inbox.MarkProcessed();

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task HandleFailureAsync(
        string json,
        Exception exception,
        byte[] body,
        IBasicProperties? properties,
        CancellationToken cancellationToken)
    {
        await TryMarkFailedAsync(json, exception, cancellationToken);

        try
        {
            var retryCount = GetRetryCount(properties);

            if (retryCount < MaxRetryCount)
            {
                RepublishWithRetry(body, retryCount + 1);
                _logger.LogWarning("Message requeued with retry count {RetryCount}", retryCount + 1);
            }
            else
            {
                PublishToDeadLetter(body);
                _logger.LogError("Message moved to DLQ after max retry.");
            }
        }
        catch (Exception retryEx)
        {
            _logger.LogError(retryEx, "Failed during retry/DLQ handling.");
        }
    }

    private async Task TryMarkFailedAsync(string json, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json);
            if (message is null)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken);
            var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
                x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
                cancellationToken);

            if (job is not null && job.Status != JobStatus.Succeeded && job.Status != JobStatus.Failed)
            {
                job.MarkFailed(exception.Message);
            }

            if (inbox is not null && inbox.Status == InboxStatus.Processing)
            {
                inbox.MarkFailed(exception.Message);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception markFailedException)
        {
            _logger.LogError(markFailedException, "Failed to mark job and inbox as failed after processing error.");
        }
    }

    private int GetRetryCount(IBasicProperties? properties)
    {
        if (properties?.Headers is null)
            return 0;

        if (!properties.Headers.TryGetValue("x-retry-count", out var value))
            return 0;

        if (value is byte[] bytes && int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed))
            return parsed;

        if (value is int intValue)
            return intValue;

        return 0;
    }

    private void RepublishWithRetry(byte[] body, int retryCount)
    {
        if (_channel is null)
            return;

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Headers = new Dictionary<string, object>
        {
            ["x-retry-count"] = retryCount.ToString()
        };

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.QueueName,
            basicProperties: properties,
            body: body);
    }

    private void PublishToDeadLetter(byte[] body)
    {
        if (_channel is null)
            return;

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.DeadLetterQueueName,
            basicProperties: properties,
            body: body);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
