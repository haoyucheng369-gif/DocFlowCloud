using System.Text;
using System.Text.Json;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocFlowCloud.Api.Realtime;

// Job 状态变化消费者：
// API 进程订阅 Worker 发出的 job.status.changed 事件，
// 再通过 SignalR 把状态变化广播给前端页面。
public sealed class JobStatusUpdatesConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;
    private readonly IHubContext<JobUpdatesHub> _hubContext;
    private readonly ILogger<JobStatusUpdatesConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public JobStatusUpdatesConsumer(
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings,
        IHubContext<JobUpdatesHub> hubContext,
        ILogger<JobStatusUpdatesConsumer> logger)
    {
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
        _hubContext = hubContext;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // API 侧消费者只需要拿到一个消费 channel，并确保状态更新队列拓扑存在。
        _connection = _connectionProvider.GetConnection();
        _channel = _connection.CreateModel();
        _topologyInitializer.EnsureTopology(_channel);

        _logger.LogInformation(
            "Job status updates consumer connected. Queue: {QueueName}",
            _settings.JobStatusUpdatesQueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            try
            {
                // 先把 MQ 里的状态变化消息还原成应用层契约。
                var message = JsonSerializer.Deserialize<JobStatusChangedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Status change message deserialization failed.");

                // API 侧只做一件事：把状态变化转成前端能监听的 SignalR 事件。
                await _hubContext.Clients.All.SendAsync(
                    "jobUpdated",
                    new
                    {
                        jobId = message.JobId,
                        status = message.Status,
                        retryCount = message.RetryCount
                    },
                    stoppingToken);

                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process job status update message: {Message}", json);
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.JobStatusUpdatesQueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Job status updates consumer started.");
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
