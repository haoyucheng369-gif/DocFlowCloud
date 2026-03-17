using System.Text;
using System.Text.Json;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocFlowCloud.Api.Realtime;

// Job 状态变化消费者：
// API 订阅 Worker 发出的 job.status.changed 事件，再通过 SignalR 推给前端。
public sealed class JobStatusUpdatesConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqSettings _settings;
    private readonly IHubContext<JobUpdatesHub> _hubContext;
    private readonly ILogger<JobStatusUpdatesConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public JobStatusUpdatesConsumer(
        RabbitMqSettings settings,
        IHubContext<JobUpdatesHub> hubContext,
        ILogger<JobStatusUpdatesConsumer> logger)
    {
        _settings = settings;
        _hubContext = hubContext;
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

        _channel.ExchangeDeclare(
            exchange: _settings.TopicExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        _channel.QueueDeclare(
            queue: _settings.JobStatusUpdatesQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.QueueBind(
            _settings.JobStatusUpdatesQueueName,
            _settings.TopicExchangeName,
            _settings.JobStatusUpdatesBindingKey);

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
                var message = JsonSerializer.Deserialize<JobStatusChangedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Status change message deserialization failed.");

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
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
