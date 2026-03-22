using RabbitMQ.Client;

namespace DocFlowCloud.Infrastructure.Messaging;

// RabbitMQ 拓扑初始化器实现：
// 当前项目需要的主队列、重试队列、DLQ、通知队列和状态更新队列都在这里统一声明。
public sealed class RabbitMqTopologyInitializer : IRabbitMqTopologyInitializer
{
    private readonly RabbitMqSettings _settings;

    public RabbitMqTopologyInitializer(RabbitMqSettings settings)
    {
        _settings = settings;
    }

    public void EnsureTopology(IModel channel)
    {
        // 核心 topic exchange：所有业务消息先发到这里，再按 routing key 路由。
        channel.ExchangeDeclare(
            exchange: _settings.TopicExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        // 最终失败消息进入 DLQ，供人工排查或补偿。
        channel.QueueDeclare(
            queue: _settings.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var retryQueueArguments = new Dictionary<string, object>
        {
            // retry queue 里的消息 TTL 到期后，再通过 dead-letter 回主交换机。
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
            // 主队列里最终处理不了的消息，dead-letter 到 DLQ。
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

        // 通知队列：由通知服务单独消费，和主业务处理解耦。
        channel.QueueDeclare(
            queue: _settings.NotificationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(_settings.NotificationQueueName, _settings.TopicExchangeName, _settings.NotificationQueueBindingKey);

        channel.QueueDeclare(
            queue: _settings.JobStatusUpdatesQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        // 状态更新队列：API 消费后，再转成 SignalR 推送给前端。
        channel.QueueBind(
            _settings.JobStatusUpdatesQueueName,
            _settings.TopicExchangeName,
            _settings.JobStatusUpdatesBindingKey);
    }
}
