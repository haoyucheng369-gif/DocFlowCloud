namespace DocFlowCloud.Application.Messaging;

// Job 创建事件：
// 这是服务间通过 RabbitMQ 传递的集成消息契约。
// 它不是 HTTP DTO，而是“Job 已创建，可以开始后台处理了”这件事的事件载体。
public sealed class JobCreatedIntegrationMessage
{
    public Guid MessageId { get; set; }
    public Guid JobId { get; set; }
    public string CorrelationId { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}
