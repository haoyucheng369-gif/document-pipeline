namespace CloudDocumentPipeline.Application.Jobs;

// Payload stored on a DocumentToPdf job. It references the uploaded file by storage key.
public sealed class DocumentToPdfJobPayload
{
    public string OriginalFileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string InputStorageKey { get; set; } = default!;
}
