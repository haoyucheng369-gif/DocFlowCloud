namespace DocFlowCloud.Application.Messaging;

public sealed class JobCreatedIntegrationMessage
{
    public Guid MessageId { get; set; }
    public Guid JobId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}
