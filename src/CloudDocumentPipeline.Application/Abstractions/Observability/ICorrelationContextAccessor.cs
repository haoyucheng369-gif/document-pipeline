namespace CloudDocumentPipeline.Application.Abstractions.Observability;

public interface ICorrelationContextAccessor
{
    string GetCorrelationId();
}
