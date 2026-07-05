namespace CloudDocumentPipeline.Application.Abstractions.Storage;

// Provider-neutral file storage abstraction.
// Callers exchange logical storage keys instead of local paths or Blob URLs.
public interface IFileStorage
{
    Task<string> SaveAsync(
        string category,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);
}
