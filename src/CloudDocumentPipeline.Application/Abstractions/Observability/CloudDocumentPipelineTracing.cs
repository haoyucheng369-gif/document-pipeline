using System.Diagnostics;

namespace CloudDocumentPipeline.Application.Abstractions.Observability;

public static class CloudDocumentPipelineTracing
{
    public const string SourceName = "CloudDocumentPipeline";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
