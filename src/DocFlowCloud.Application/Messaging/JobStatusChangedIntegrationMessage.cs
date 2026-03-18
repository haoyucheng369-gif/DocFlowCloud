namespace DocFlowCloud.Application.Messaging;

// Job 状态变化事件：
// 由 Worker 在任务成功或失败后发布，供 API 实时层订阅并转成 SignalR 推送。
public sealed class JobStatusChangedIntegrationMessage
{
    public Guid MessageId { get; set; }
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
