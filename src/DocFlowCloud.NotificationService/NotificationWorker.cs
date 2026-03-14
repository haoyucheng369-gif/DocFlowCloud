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
    // Notification consumer 和 Job consumer 处理的是同一个事件，
    // 但 Inbox 去重必须按消费者区分，所以这里要有独立的 ConsumerName。
    private const string ConsumerName = "DocFlowCloud.NotificationConsumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<NotificationWorker> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public NotificationWorker(
        IServiceScopeFactory scopeFactory,
        RabbitMqSettings settings,
        ILogger<NotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Notification service 只订阅通知相关队列，不负责 retry / DLQ 编排。
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(
            exchange: _settings.TopicExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        _channel.QueueDeclare(
            queue: _settings.NotificationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        _channel.QueueBind(_settings.NotificationQueueName, _settings.TopicExchangeName, _settings.NotificationQueueBindingKey);

        // 控制并发抓取量，避免一次在本地积压太多未确认消息。
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _logger.LogInformation("Notification worker connected. Queue: {QueueName}", _settings.NotificationQueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        // 和 Job worker 一样，Notification consumer 也是事件驱动模式，不会主动轮询队列。
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            // 原始消息先还原成 JSON，再反序列化成契约对象。
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            try
            {
                var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json)
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

                    // 通知成功后，补齐 Inbox 最终状态。
                    inbox.MarkProcessed();
                    await inboxRepository.SaveChangesAsync(stoppingToken);

                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    _logger.LogInformation("Notification sent for job {JobId}.", message.JobId);
                }
            }
            catch (Exception ex)
            {
                // 通知服务当前没有像 Job worker 一样做 retry / DLQ，
                // 这里只记录错误并 ACK，避免队列里无限重复。
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
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
