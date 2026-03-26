namespace DocFlowCloud.Infrastructure.Messaging;

// 消息基础设施总开关：
// 用来决定当前环境走 RabbitMq 还是 Azure Service Bus。
// 这样本地可以继续保留 RabbitMQ 调试体验，testbed/prod 再切到云上消息服务。
public sealed class MessagingSettings
{
    public const string SectionName = "Messaging";

    public string Provider { get; set; } = "RabbitMq";
}
