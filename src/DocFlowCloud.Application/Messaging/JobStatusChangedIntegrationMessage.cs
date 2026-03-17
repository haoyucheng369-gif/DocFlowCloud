namespace DocFlowCloud.Application.Messaging;

// Job 状态变化事件：
// 由 Worker 在任务状态发生变化后发布，供实时推送层订阅。
public sealed class JobStatusChangedIntegrationMessage
{
    public Guid MessageId { get; set; }
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
