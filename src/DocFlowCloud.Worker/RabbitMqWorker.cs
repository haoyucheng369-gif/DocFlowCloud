using System.Text;
using System.Text.Json;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Inbox;
using DocFlowCloud.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

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

                using var scope = _scopeFactory.CreateScope();
                var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();
                var jobService = scope.ServiceProvider.GetRequiredService<JobService>();

                var alreadyProcessed = await inboxRepository.ExistsAsync(
                    message.MessageId,
                    ConsumerName,
                    stoppingToken);

                if (alreadyProcessed)
                {
                    _logger.LogWarning("Duplicate message ignored. MessageId: {MessageId}", message.MessageId);
                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    return;
                }

                await jobService.MarkProcessingAsync(message.JobId, stoppingToken);

                await Task.Delay(3000, stoppingToken);

                var resultJson = JsonSerializer.Serialize(new
                {
                    processedAtUtc = DateTime.UtcNow,
                    status = "OK"
                });

                await jobService.MarkSucceededAsync(message.JobId, resultJson, stoppingToken);

                await inboxRepository.AddAsync(new InboxMessage(message.MessageId, ConsumerName), stoppingToken);
                await inboxRepository.SaveChangesAsync(stoppingToken);

                _channel.BasicAck(eventArgs.DeliveryTag, false);

                _logger.LogInformation("Job {JobId} processed successfully.", message.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing message.");

                try
                {
                    var retryCount = GetRetryCount(eventArgs.BasicProperties);

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

                _channel?.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("RabbitMQ consumer started.");

        return Task.CompletedTask;
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
        if (_channel is null) return;

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
        if (_channel is null) return;

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