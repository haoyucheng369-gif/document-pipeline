using CloudDocumentPipeline.Application.Abstractions.Observability;

namespace CloudDocumentPipeline.Api.Observability;

public sealed class HttpCorrelationContextAccessor : ICorrelationContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCorrelationContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCorrelationId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Guid.NewGuid().ToString("N");
        }

        if (httpContext.Items.TryGetValue(CorrelationConstants.HeaderName, out var value) &&
            value is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return httpContext.TraceIdentifier;
    }
}
