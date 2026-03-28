using System.Text;
using Azure.Messaging.ServiceBus;
using DocFlowCloud.Application.Abstractions.Messaging;

namespace DocFlowCloud.Infrastructure.Messaging;

// Service Bus 发送侧实现：
// 只负责把 Outbox 里的集成消息发到 Azure Service Bus Topic。
// 当前先保留原有 IJobMessagePublisher 接口，这样上层业务和 OutboxPublisherWorker 不需要改动。
public sealed class ServiceBusJobMessagePublisher : IJobMessagePublisher
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSettings _settings;

    public ServiceBusJobMessagePublisher(
        ServiceBusClient serviceBusClient,
        ServiceBusSettings settings)
    {
        _serviceBusClient = serviceBusClient;
        _settings = settings;
    }

    public Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use PublishRawAsync for integration messages.");
    }

    public async Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default)
    {
        // 每次发送创建短生命周期 sender。
        // 先求切换简单清晰，后面如果确认有明显性能压力，再考虑做 sender 复用。
        await using var sender = _serviceBusClient.CreateSender(_settings.TopicName);

        // 把消息类型同时放到 Subject 和应用属性里：
        // Subject 方便 Service Bus 侧观察，ApplicationProperties 方便后续兼容读取。
        var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(payloadJson))
        {
            Subject = messageType,
            ContentType = "application/json"
        };

        message.ApplicationProperties["messageType"] = messageType;

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
