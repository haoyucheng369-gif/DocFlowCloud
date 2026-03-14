using DocFlowCloud.Application.Abstractions.Observability;
using System.Text.Json;
using DocFlowCloud.Application.Abstractions.Processing;
using Microsoft.Extensions.Logging;

namespace DocFlowCloud.Worker;

// 副作用执行器：
// 这里代表真正的“外部动作”，例如调用第三方 API、文件处理、OCR、发送 webhook 等。
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
        // 执行副作用前先把关键上下文打进日志，方便排查外部调用问题。
        _logger.LogInformation(
            "Executing external side effect. JobId: {JobId}, JobType: {JobType}, IdempotencyKey: {IdempotencyKey}, CorrelationId: {CorrelationId}",
            jobId,
            jobType,
            idempotencyKey,
            correlationId);

        // 这里模拟“发给第三方时应该带哪些技术性 header”。
        // 以后如果真的发 HTTP 请求，CorrelationId 就通过这个头继续向下游传递。
        var outboundHeaders = new Dictionary<string, string>
        {
            [CorrelationConstants.HeaderName] = correlationId
        };

        // 当前只是模拟一个耗时副作用，后面可以替换成真实外部调用。
        await Task.Delay(3000, cancellationToken);

        // 返回结果同样做成结构化 JSON，方便任务详情页或日志查看。
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
