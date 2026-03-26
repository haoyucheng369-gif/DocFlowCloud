namespace DocFlowCloud.Infrastructure.Messaging;

// Azure Service Bus 配置：
// 当前先按 Topic + Subscription 模型设计，分别给 worker / notification / api-realtime 使用。
// 这样能比较接近原来 RabbitMQ 一条消息被多个消费者分别处理的语义。
public sealed class ServiceBusSettings
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public string TopicName { get; set; } = "job-events";
    public string WorkerSubscriptionName { get; set; } = "worker";
    public string NotificationSubscriptionName { get; set; } = "notification";
    public string ApiRealtimeSubscriptionName { get; set; } = "api-realtime";
}
