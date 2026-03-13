using System.Text.Json;
using DocFlowCloud.Application.Abstractions.Processing;
using Microsoft.Extensions.Logging;

namespace DocFlowCloud.Worker;

public sealed class JobSideEffectExecutor : IJobSideEffectExecutor
{
    private readonly ILogger<JobSideEffectExecutor> _logger;

    public JobSideEffectExecutor(ILogger<JobSideEffectExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        Guid jobId,
        string jobType,
        string payloadJson,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing external side effect. JobId: {JobId}, JobType: {JobType}, IdempotencyKey: {IdempotencyKey}",
            jobId,
            jobType,
            idempotencyKey);

        await Task.Delay(3000, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            processedAtUtc = DateTime.UtcNow,
            status = "OK",
            jobType,
            payloadJson,
            idempotencyKey
        });
    }
}
