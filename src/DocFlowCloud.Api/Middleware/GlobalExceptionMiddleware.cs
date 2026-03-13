using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace DocFlowCloud.Api.Middleware;

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

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["correlationId"] =
            context.Items.TryGetValue(CorrelationConstants.HeaderName, out var correlationId)
                ? correlationId
                : context.TraceIdentifier;
        problemDetails.Instance = context.Request.Path;

        return problemDetails;
    }
}
