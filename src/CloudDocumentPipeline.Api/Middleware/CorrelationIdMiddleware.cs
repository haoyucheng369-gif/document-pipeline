using CloudDocumentPipeline.Application.Abstractions.Observability;
using Serilog.Context;

namespace CloudDocumentPipeline.Api.Middleware;

// Ensures every request has a correlation ID shared by responses, logs, and downstream services.
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

        // Store the ID in HttpContext for application services and return it to the client.
        context.Items[CorrelationConstants.HeaderName] = correlationId;
        context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;

        // Serilog enriches all logs emitted within the request with the same correlation ID.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // Preserve a caller-provided ID when present so retries can be traced end to end.
        if (context.Request.Headers.TryGetValue(CorrelationConstants.HeaderName, out var values))
        {
            var incoming = values.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                return incoming;
            }
        }

        // Fall back to ASP.NET Core's request trace identifier.
        return context.TraceIdentifier;
    }
}
