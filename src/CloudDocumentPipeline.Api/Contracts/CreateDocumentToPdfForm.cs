namespace CloudDocumentPipeline.Api.Contracts;

// Multipart form model for document-to-PDF uploads.
// Keeping IFormFile on a dedicated model produces a stable Swagger/OpenAPI contract.
public sealed class CreateDocumentToPdfForm
{
    public IFormFile File { get; set; } = default!;
    public string? Name { get; set; }
}
