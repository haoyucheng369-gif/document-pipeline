using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CloudDocumentPipeline.Application.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace CloudDocumentPipeline.Infrastructure.Storage;

// Azure Blob implementation used by testbed and production runtimes.
public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobFileStorage> _logger;

    public AzureBlobFileStorage(StorageSettings settings, ILogger<AzureBlobFileStorage> logger)
    {
        _logger = logger;

        if (string.IsNullOrWhiteSpace(settings.AzureBlob.ConnectionString))
        {
            throw new InvalidOperationException("Storage:AzureBlob:ConnectionString is required when Provider is AzureBlob.");
        }

        if (string.IsNullOrWhiteSpace(settings.AzureBlob.ContainerName))
        {
            throw new InvalidOperationException("Storage:AzureBlob:ContainerName is required when Provider is AzureBlob.");
        }

        // The container is private; downloads are served through the API after job authorization checks.
        _containerClient = new BlobContainerClient(
            settings.AzureBlob.ConnectionString,
            settings.AzureBlob.ContainerName);

        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> SaveAsync(
        string category,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        // Use the same logical key shape as local storage so the application stays provider-neutral.
        var storageKey = BuildStorageKey(category, fileName);
        var blobClient = _containerClient.GetBlobClient(storageKey);

        await using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

        _logger.LogInformation("Stored file in Azure Blob. StorageKey: {StorageKey}", storageKey);
        return storageKey;
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        using var memoryStream = new MemoryStream();
        await blobClient.DownloadToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private static string BuildStorageKey(string category, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        // Add a unique suffix so uploads with the same original name do not overwrite each other.
        var safeFileName = $"{baseName}-{Guid.NewGuid():N}{extension}";

        return string.Join('/',
            category.Trim().ToLowerInvariant(),
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            DateTime.UtcNow.ToString("dd"),
            safeFileName);
    }
}
