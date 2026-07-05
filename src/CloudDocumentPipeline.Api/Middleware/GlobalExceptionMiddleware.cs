using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace CloudDocumentPipeline.Api.Middleware;

// Converts uncaught exceptions into consistent RFC 7807-style ProblemDetails responses.
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
            // Log the original exception server-side; clients receive a controlled problem payload.
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
        // Domain exceptions map to stable HTTP status codes; unknown failures stay generic.
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

        // Diagnostics are always included so frontend errors can be tied back to server logs.
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["correlationId"] =
            context.Items.TryGetValue(CorrelationConstants.HeaderName, out var correlationId)
                ? correlationId
                : context.TraceIdentifier;
        problemDetails.Instance = context.Request.Path;

        return problemDetails;
    }
}
