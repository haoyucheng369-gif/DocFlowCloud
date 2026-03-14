using DocFlowCloud.Application.Abstractions.Observability;
using Serilog.Context;

namespace DocFlowCloud.Api.Middleware;

// 相关性标识中间件：
// 给每个 HTTP 请求分配一个 CorrelationId，并把它贯穿到日志和响应头里。
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 优先沿用上游传来的 CorrelationId；没有就自己生成一个。
        var correlationId = GetOrCreateCorrelationId(context);

        // 放到 HttpContext.Items 里，后续服务和中间件可以统一读取。
        context.Items[CorrelationConstants.HeaderName] = correlationId;
        // 回写到响应头，便于前端或调用方拿到这条链路编号。
        context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;

        // 压入日志上下文，后续本次请求内的日志都能自动带上这个属性。
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // 如果上游已经传了 X-Correlation-Id，就沿用它。
        if (context.Request.Headers.TryGetValue(CorrelationConstants.HeaderName, out var values))
        {
            var incoming = values.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                return incoming;
            }
        }

        // 否则退回到 ASP.NET Core 自带的 TraceIdentifier。
        return context.TraceIdentifier;
    }
}
