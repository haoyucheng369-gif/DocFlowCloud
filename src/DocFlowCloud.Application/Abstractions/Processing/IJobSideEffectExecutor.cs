namespace DocFlowCloud.Application.Abstractions.Processing;

public interface IJobSideEffectExecutor
{
    Task<string> ExecuteAsync(Guid jobId, string jobType, string payloadJson, string idempotencyKey, CancellationToken cancellationToken = default);
}
