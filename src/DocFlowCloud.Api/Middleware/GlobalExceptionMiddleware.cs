using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace DocFlowCloud.Api.Middleware;

// 全局异常中间件：
// 统一把未处理异常转换成 ProblemDetails，避免每个控制器自己写 try/catch。
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // 所有未处理异常先统一记日志，再转换成标准 HTTP 错误响应。
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);

            var problem = CreateProblemDetails(ex, context);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = problem.Status ?? (int)HttpStatusCode.InternalServerError;

            var payload = JsonSerializer.Serialize(problem);
            await context.Response.WriteAsync(payload);
        }
    }

    private static ProblemDetails CreateProblemDetails(Exception exception, HttpContext context)
    {
        // 这里按异常类型映射成更合理的 HTTP 状态码，
        // 让调用方拿到的不是笼统的 500。
        var problemDetails = exception switch
        {
            JobNotFoundException => new ProblemDetails
            {
                Title = "Job not found",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound
            },
            InvalidJobStateException => new ProblemDetails
            {
                Title = "Invalid job state",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },
            _ => new ProblemDetails
            {
                Title = "An unexpected error occurred.",
                Detail = "Use the trace identifier for diagnostics.",
                Status = StatusCodes.Status500InternalServerError
            }
        };

        // traceId / correlationId 一并返回，方便用户把错误编号反馈给你排查。
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["correlationId"] =
            context.Items.TryGetValue(CorrelationConstants.HeaderName, out var correlationId)
                ? correlationId
                : context.TraceIdentifier;
        problemDetails.Instance = context.Request.Path;

        return problemDetails;
    }
}
