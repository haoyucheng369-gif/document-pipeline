using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace CloudDocumentPipeline.Api.Middleware;

// 鍏ㄥ眬寮傚父涓棿浠讹細
// 缁熶竴鎶婃湭澶勭悊寮傚父杞崲鎴?ProblemDetails锛岄伩鍏嶆瘡涓帶鍒跺櫒鑷繁鍐?try/catch銆?
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
            // 鎵€鏈夋湭澶勭悊寮傚父鍏堢粺涓€璁版棩蹇楋紝鍐嶈浆鎹㈡垚鏍囧噯 HTTP 閿欒鍝嶅簲銆?
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
        // 杩欓噷鎸夊紓甯哥被鍨嬫槧灏勬垚鏇村悎鐞嗙殑 HTTP 鐘舵€佺爜锛?
        // 璁╄皟鐢ㄦ柟鎷垮埌鐨勪笉鏄缁熺殑 500銆?
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

        // traceId / correlationId 涓€骞惰繑鍥烇紝鏂逛究鐢ㄦ埛鎶婇敊璇紪鍙峰弽棣堢粰浣犳帓鏌ャ€?
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["correlationId"] =
            context.Items.TryGetValue(CorrelationConstants.HeaderName, out var correlationId)
                ? correlationId
                : context.TraceIdentifier;
        problemDetails.Instance = context.Request.Path;

        return problemDetails;
    }
}
