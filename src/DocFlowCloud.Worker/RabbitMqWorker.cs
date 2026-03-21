using System.Text;
using System.Text.Json;
using DocFlowCloud.Application.Abstractions.Messaging;
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
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    // 同一条消息会被不同 consumer 分别处理，所以这里的消费者名称要固定。
    // Inbox 去重与 claim 抢占依赖 (MessageId, ConsumerName) 这组唯一键。
    private const string ConsumerName = "DocFlowCloud.JobConsumer";
    // 超过最大重试次数后，这条消息不再回主流程，而是进入 DLQ 等待排查。
    private const int MaxRetryCount = 3;
    // 简单阶梯式 backoff：第 1/2/3 次重试分别延迟 1s / 5s / 30s。
    private static readonly int[] RetryDelaysInSeconds = [1, 5, 30];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqWorker> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings,
        ILogger<RabbitMqWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // StartAsync 只负责准备 RabbitMQ 消费通道。
        // 长连接交给公共 ConnectionProvider 复用，拓扑声明交给公共初始化器。
        _connection = _connectionProvider.GetConnection();
        _channel = _connection.CreateModel();
        _topologyInitializer.EnsureTopology(_channel);

        // 限制单个 consumer 同时抓取在手上的未确认消息数，避免瞬间拉太多消息。
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _logger.LogInformation("RabbitMQ worker connected. Queue: {QueueName}", _settings.QueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        // EventingBasicConsumer 是“消息到了再回调我”的事件驱动模式。
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            // 先拿到 RabbitMQ 里的原始消息体，后面所有处理都基于这份 JSON。
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            _logger.LogInformation("Received message: {Message}", json);

            try
            {
                // 先把消息体还原成应用层契约对象，后面业务逻辑都围绕这个对象展开。
                var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Message deserialization failed.");

                // 把 CorrelationId 压进日志上下文，后续这条消息处理链上的日志就能串起来。
                using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();

                    // 先 claim 再处理：只有抢到处理权的实例才能继续执行业务逻辑。
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

                    // 如果业务已经成功过了，只补齐 Inbox 状态，不再重复执行副作用。
                    var completion = await TryCompleteAlreadySucceededJobAsync(message, stoppingToken);
                    if (completion)
                    {
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        _logger.LogInformation("Job {JobId} was already succeeded. Side effects were skipped.", message.JobId);
                        return;
                    }

                    // 真正的业务副作用放在独立服务里执行，Worker 这里只负责编排与状态控制。
                    var resultJson = await ExecuteSideEffectsAsync(message, stoppingToken);

                    // 成功路径里把 Job 和 Inbox 一起提交，避免状态不一致。
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
        // 业务幂等保护：
        // 如果 Job 已经成功，则只需要把当前 Inbox 从 Processing 补成 Processed。
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
        // 成功路径的数据库事务边界：
        // Job 状态更新 + Inbox 状态更新必须一起提交。
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
            // 双保险：即使到这里才发现 Job 已成功，也不要重复执行业务完成逻辑。
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        // 只有 Pending -> Processing -> Succeeded 这条合法路径才能走到这里。
        job.MarkProcessing();
        job.MarkSucceeded(resultJson);
        inbox.MarkProcessed();

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await PublishJobStatusChangedAsync(
            message.JobId,
            JobStatus.Succeeded,
            job.RetryCount,
            message.CorrelationId,
            cancellationToken);
    }

    private async Task HandleFailureAsync(
        string json,
        Exception exception,
        byte[] body,
        IBasicProperties? properties,
        CancellationToken cancellationToken)
    {
        // 失败处理分两层：
        // 1. 先把数据库里的 Job / Inbox 状态修正为失败
        // 2. 再决定消息层是重试还是进入 DLQ
        await TryMarkFailedAsync(json, exception, cancellationToken);

        try
        {
            var disposition = MessageFailureClassification.Classify(exception);
            var retryCount = GetRetryCount(properties);

            if (disposition == MessageFailureDisposition.Retry && retryCount < MaxRetryCount)
            {
                // 可重试错误走 backoff：消息先进入 retry queue，过 TTL 再回主队列。
                RepublishWithRetry(body, retryCount + 1);
                _logger.LogWarning(
                    "Retryable error. Message scheduled for retry {RetryCount} after {DelaySeconds}s",
                    retryCount + 1,
                    GetRetryDelaySeconds(retryCount + 1));
            }
            else
            {
                // 不可重试，或重试次数已用尽，转入 DLQ。
                PublishToDeadLetter(body);
                _logger.LogError(
                    "Message moved to DLQ. Disposition: {Disposition}, RetryCount: {RetryCount}",
                    disposition,
                    retryCount);
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
            // 尽量把失败状态落回数据库，避免 Job / Inbox 一直卡在 Processing。
            var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions);
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

            if (job is not null && job.Status == JobStatus.Failed)
            {
                await PublishJobStatusChangedAsync(
                    job.Id,
                    JobStatus.Failed,
                    job.RetryCount,
                    message.CorrelationId,
                    cancellationToken);
            }
        }
        catch (Exception markFailedException)
        {
            _logger.LogError(markFailedException, "Failed to mark job and inbox as failed after processing error.");
        }
    }

    private async Task PublishJobStatusChangedAsync(
        Guid jobId,
        JobStatus status,
        int retryCount,
        string correlationId,
        CancellationToken cancellationToken)
    {
        // 状态变化通知不是主业务消息，所以这里直接复用统一发布器发送。
        using var scope = _scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IJobMessagePublisher>();

        var message = new JobStatusChangedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = jobId,
            Status = status.ToString(),
            RetryCount = retryCount,
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        };

        var payloadJson = JsonSerializer.Serialize(message, JsonSerializerOptions);

        await publisher.PublishRawAsync(
            nameof(JobStatusChangedIntegrationMessage),
            payloadJson,
            cancellationToken);
    }

    private int GetRetryCount(IBasicProperties? properties)
    {
        // 重试次数存放在 RabbitMQ header 里，而不是消息体里。
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

        // 延迟重试：
        // 先发到 retry queue，并设置 TTL；
        // TTL 到期后，RabbitMQ 会通过 dead-letter 把消息重新路由回主队列。
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Expiration = TimeSpan.FromSeconds(GetRetryDelaySeconds(retryCount)).TotalMilliseconds.ToString("F0");
        properties.Headers = new Dictionary<string, object>
        {
            ["x-retry-count"] = retryCount.ToString()
        };

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.RetryQueueName,
            basicProperties: properties,
            body: body);
    }

    private static int GetRetryDelaySeconds(int retryCount)
    {
        var index = Math.Clamp(retryCount - 1, 0, RetryDelaysInSeconds.Length - 1);
        return RetryDelaysInSeconds[index];
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
        // Hosted service 退出时只关闭当前 channel。
        // 长连接由 ConnectionProvider 按进程统一复用和释放。
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
