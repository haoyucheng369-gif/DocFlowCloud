using System.Text;
using System.Text.Json;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Inbox;
using DocFlowCloud.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace DocFlowCloud.NotificationService;

public sealed class NotificationWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    // Notification consumer 和 Job consumer 处理的是同一条事件，
    // 但 Inbox 去重必须按消费者区分，所以这里要有独立的 ConsumerName。
    private const string ConsumerName = "DocFlowCloud.NotificationConsumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<NotificationWorker> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public NotificationWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings,
        ILogger<NotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Notification service 只订阅通知相关队列，不负责 retry / DLQ 编排。
        _connection = _connectionProvider.GetConnection();
        _channel = _connection.CreateModel();
        _topologyInitializer.EnsureTopology(_channel);

        // 控制并发抓取量，避免一次在本地积压太多未确认消息。
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _logger.LogInformation("Notification worker connected. Queue: {QueueName}", _settings.NotificationQueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            // 原始消息先还原成 JSON，再反序列化成契约对象。
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            try
            {
                var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Notification message deserialization failed.");

                using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();
                    var sender = scope.ServiceProvider.GetRequiredService<NotificationEmailSender>();

                    // 通知消息同样先 claim，避免重复发送通知。
                    var claimed = await inboxRepository.TryClaimAsync(
                        message.MessageId,
                        ConsumerName,
                        TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds),
                        stoppingToken);

                    if (!claimed)
                    {
                        _logger.LogWarning("Notification message already claimed or processed. MessageId: {MessageId}", message.MessageId);
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        return;
                    }

                    var inbox = await inboxRepository.GetByMessageIdAsync(message.MessageId, ConsumerName, stoppingToken)
                        ?? throw new InvalidOperationException($"Inbox claim for notification message '{message.MessageId}' was not found.");

                    // 当前实现是模拟发邮件，后续可以替换成真实邮件服务或 webhook。
                    await sender.SendAsync(message, stoppingToken);

                    // 通知成功后补齐 Inbox 最终状态。
                    inbox.MarkProcessed();
                    await inboxRepository.SaveChangesAsync(stoppingToken);

                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    _logger.LogInformation("Notification sent for job {JobId}.", message.JobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing notification message.");
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.NotificationQueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Notification consumer started.");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        // 这里只释放当前 channel，长连接由 ConnectionProvider 统一管理。
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
