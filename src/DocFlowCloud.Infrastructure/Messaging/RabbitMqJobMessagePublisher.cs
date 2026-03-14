using System.Text;
using DocFlowCloud.Application.Abstractions.Messaging;
using RabbitMQ.Client;

namespace DocFlowCloud.Infrastructure.Messaging;

// RabbitMQ 发布器：
// 负责把 Outbox 里的消息真正发布到 topic exchange。
public sealed class RabbitMqJobMessagePublisher : IJobMessagePublisher
{
    private readonly RabbitMqSettings _settings;

    public RabbitMqJobMessagePublisher(RabbitMqSettings settings)
    {
        _settings = settings;
    }

    public Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // 当前项目统一通过 PublishRawAsync 发集成消息，这个旧接口保留但不再使用。
        throw new NotSupportedException("Use PublishRawAsync for integration messages.");
    }

    public Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default)
    {
        // 发布器不依赖长期连接池，当前实现每次发布时临时创建连接和 channel。
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // 先确保交换机、队列和绑定关系存在，再执行发布。
        DeclareMessagingTopology(channel);

        var body = Encoding.UTF8.GetBytes(payloadJson);
        var properties = channel.CreateBasicProperties();
        // 持久化消息，避免 broker 重启后直接丢失。
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
        // 这一段是“消息拓扑初始化”，负责把当前系统需要的 exchange / queue / binding 全部声明好。
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
            // retry queue 里的消息 TTL 到期后，再通过 dead-letter 路由回主交换机。
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
            // 主队列里最终处理不了的消息直接进入 DLQ。
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
        // 当前只有 JobCreatedIntegrationMessage 这一种业务消息。
        // 后面如果扩展更多事件类型，就在这里补映射关系。
        return messageType switch
        {
            nameof(Application.Messaging.JobCreatedIntegrationMessage) => _settings.JobCreatedRoutingKey,
            _ => throw new NotSupportedException($"Unsupported integration message type '{messageType}'.")
        };
    }
}
