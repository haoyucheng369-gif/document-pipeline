namespace CloudDocumentPipeline.Application.Jobs;

// Result metadata written after PDF generation. The PDF bytes stay in file storage.
public sealed class DocumentToPdfJobResult
{
    public string OutputFileName { get; set; } = default!;
    public string OutputStorageKey { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
}
