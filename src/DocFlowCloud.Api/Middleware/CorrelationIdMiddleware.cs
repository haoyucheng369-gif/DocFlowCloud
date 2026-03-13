using DocFlowCloud.Application.Abstractions.Observability;
using Serilog.Context;

namespace DocFlowCloud.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.Items[CorrelationConstants.HeaderName] = correlationId;
        context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationConstants.HeaderName, out var values))
        {
            var incoming = values.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                return incoming;
            }
        }

        return context.TraceIdentifier;
    }
}
