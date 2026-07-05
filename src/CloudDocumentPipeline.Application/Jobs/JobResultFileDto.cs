namespace CloudDocumentPipeline.Application.Jobs;

// Application DTO used by the API to return a downloadable result file.
public sealed class JobResultFileDto
{
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
}
