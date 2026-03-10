using System.Text;
using System.Text.Json;
using DocFlowCloud.Application.Abstractions.Messaging;
using RabbitMQ.Client;

namespace DocFlowCloud.Infrastructure.Messaging;

public sealed class RabbitMqJobMessagePublisher : IJobMessagePublisher
{
    private readonly RabbitMqSettings _settings;

    public RabbitMqJobMessagePublisher(RabbitMqSettings settings)
    {
        _settings = settings;
    }

    public Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var payload = JsonSerializer.Serialize(new { jobId });
        var body = Encoding.UTF8.GetBytes(payload);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.QueueName,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }
}