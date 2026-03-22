using System.Text;
using DocFlowCloud.Application.Abstractions.Messaging;
using RabbitMQ.Client;

namespace DocFlowCloud.Infrastructure.Messaging;

// RabbitMQ 发布器：
// 负责把 Outbox 里的集成消息真正发布到 topic exchange。
// 当前版本改成复用进程内长连接，只为每次发布创建短生命周期 channel。
public sealed class RabbitMqJobMessagePublisher : IJobMessagePublisher
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;

    public RabbitMqJobMessagePublisher(
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings)
    {
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
    }

    public Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // 当前项目统一通过 PublishRawAsync 发布集成消息，这个旧接口保留但不再使用。
        throw new NotSupportedException("Use PublishRawAsync for integration messages.");
    }

    public Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default)
    {
        // 每次发布只创建一个短生命周期 channel；
        // 长连接本身由连接提供器统一复用。
        using var channel = _connectionProvider.GetConnection().CreateModel();

        // 发布前确保拓扑存在，避免某个服务先启动时队列还没被声明。
        _topologyInitializer.EnsureTopology(channel);

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

    private string ResolveRoutingKey(string messageType)
    {
        // 这里负责把“消息类型”映射成“RabbitMQ routing key”。
        return messageType switch
        {
            nameof(Application.Messaging.JobCreatedIntegrationMessage) => _settings.JobCreatedRoutingKey,
            nameof(Application.Messaging.JobStatusChangedIntegrationMessage) => _settings.JobStatusChangedRoutingKey,
            _ => throw new NotSupportedException($"Unsupported integration message type '{messageType}'.")
        };
    }
}
