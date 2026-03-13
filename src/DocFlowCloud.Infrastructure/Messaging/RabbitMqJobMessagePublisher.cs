using System.Text;
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
        throw new NotSupportedException("Use PublishRawAsync for integration messages.");
    }

    public Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default)
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

        DeclareMessagingTopology(channel);

        var body = Encoding.UTF8.GetBytes(payloadJson);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        channel.BasicPublish(
            exchange: _settings.TopicExchangeName,
            routingKey: ResolveRoutingKey(messageType),
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    private void DeclareMessagingTopology(IModel channel)
    {
        channel.ExchangeDeclare(
            exchange: _settings.TopicExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: _settings.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var retryQueueArguments = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = _settings.TopicExchangeName,
            ["x-dead-letter-routing-key"] = _settings.JobCreatedRoutingKey
        };

        channel.QueueDeclare(
            queue: _settings.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryQueueArguments);

        var mainQueueArguments = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = _settings.DeadLetterQueueName
        };

        channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments);
        channel.QueueBind(_settings.QueueName, _settings.TopicExchangeName, _settings.JobQueueBindingKey);

        channel.QueueDeclare(
            queue: _settings.NotificationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(_settings.NotificationQueueName, _settings.TopicExchangeName, _settings.NotificationQueueBindingKey);
    }

    private string ResolveRoutingKey(string messageType)
    {
        return messageType switch
        {
            nameof(Application.Messaging.JobCreatedIntegrationMessage) => _settings.JobCreatedRoutingKey,
            _ => throw new NotSupportedException($"Unsupported integration message type '{messageType}'.")
        };
    }
}
