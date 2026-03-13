using DocFlowCloud.Application.Abstractions.Observability;
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
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing external side effect. JobId: {JobId}, JobType: {JobType}, IdempotencyKey: {IdempotencyKey}, CorrelationId: {CorrelationId}",
            jobId,
            jobType,
            idempotencyKey,
            correlationId);

        // When this becomes a real outbound HTTP call, propagate the correlation id as X-Correlation-Id.
        var outboundHeaders = new Dictionary<string, string>
        {
            [CorrelationConstants.HeaderName] = correlationId
        };

        await Task.Delay(3000, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            processedAtUtc = DateTime.UtcNow,
            status = "OK",
            jobType,
            payloadJson,
            idempotencyKey,
            correlationId,
            outboundHeaders
        });
    }
}
