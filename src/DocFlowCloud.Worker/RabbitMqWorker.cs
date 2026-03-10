using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocFlowCloud.Worker;

public sealed class RabbitMqWorker : BackgroundService
{
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
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

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
                var message = JsonSerializer.Deserialize<JobCreatedMessage>(json)
                              ?? throw new InvalidOperationException("Message deserialization failed.");

                using var scope = _scopeFactory.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<JobService>();

                await jobService.MarkProcessingAsync(message.JobId, stoppingToken);

                await Task.Delay(3000, stoppingToken);

                var resultJson = JsonSerializer.Serialize(new
                {
                    processedAtUtc = DateTime.UtcNow,
                    status = "OK"
                });

                await jobService.MarkSucceededAsync(message.JobId, resultJson, stoppingToken);

                _channel.BasicAck(eventArgs.DeliveryTag, multiple: false);

                _logger.LogInformation("Job {JobId} processed successfully.", message.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing message.");

                try
                {
                    var fallbackMessage = JsonSerializer.Deserialize<JobCreatedMessage>(json);
                    if (fallbackMessage is not null)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var jobService = scope.ServiceProvider.GetRequiredService<JobService>();
                        await jobService.MarkFailedAsync(fallbackMessage.JobId, ex.Message, stoppingToken);
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to mark job as failed.");
                }

                _channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("RabbitMQ consumer started.");

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

    private sealed class JobCreatedMessage
    {
        [JsonPropertyName("jobId")]
        public Guid JobId { get; set; }
    }
}