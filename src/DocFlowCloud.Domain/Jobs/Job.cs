namespace DocFlowCloud.Domain.Jobs;

public class Job
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Type { get; private set; } = default!;
    public JobStatus Status { get; private set; }
    public string PayloadJson { get; private set; } = default!;
    public string? ResultJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private Job() { }

    public Job(string name, string type, string payloadJson)
    {
        Id = Guid.NewGuid();
        Name = name;
        Type = type;
        PayloadJson = payloadJson;
        Status = JobStatus.Pending;
        RetryCount = 0;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProcessing()
    {
        if (Status != JobStatus.Pending)
            throw new InvalidOperationException("Only pending jobs can start processing.");

        Status = JobStatus.Processing;
        StartedAtUtc = DateTime.UtcNow;
        CompletedAtUtc = null;
        ErrorMessage = null;
        ResultJson = null;
    }

    public void MarkSucceeded(string resultJson)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException("Only processing jobs can be marked as succeeded.");

        Status = JobStatus.Succeeded;
        ResultJson = resultJson;
        CompletedAtUtc = DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        if (Status != JobStatus.Pending && Status != JobStatus.Processing)
            throw new InvalidOperationException("Only pending or processing jobs can be marked as failed.");

        Status = JobStatus.Failed;
        ErrorMessage = errorMessage;
        ResultJson = null;
        RetryCount++;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Retry()
    {
        if (Status != JobStatus.Failed)
            throw new InvalidOperationException("Only failed jobs can be retried.");

        Status = JobStatus.Pending;
        ErrorMessage = null;
        ResultJson = null;
        StartedAtUtc = null;
        CompletedAtUtc = null;
    }
}
