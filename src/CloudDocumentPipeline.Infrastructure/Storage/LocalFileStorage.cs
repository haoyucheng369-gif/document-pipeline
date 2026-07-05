using CloudDocumentPipeline.Application.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace CloudDocumentPipeline.Infrastructure.Storage;

// Local filesystem storage used for development and container-compose runs.
public sealed class LocalFileStorage : IFileStorage
{
    private readonly ILogger<LocalFileStorage> _logger;
    private readonly string _rootPath;

    public LocalFileStorage(StorageSettings settings, ILogger<LocalFileStorage> logger)
    {
        _logger = logger;
        _rootPath = ResolveRootPath(settings.Local.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        string category,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        // Storage keys are relative and portable; absolute paths never leave this provider.
        var storageKey = BuildStorageKey(category, fileName);
        var absolutePath = GetAbsolutePath(storageKey);
        var directory = Path.GetDirectoryName(absolutePath)!;

        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(absolutePath, content, cancellationToken);

        _logger.LogInformation("Stored file locally. StorageKey: {StorageKey}", storageKey);
        return storageKey;
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var absolutePath = GetAbsolutePath(storageKey);
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(absolutePath, cancellationToken);
    }

    private string GetAbsolutePath(string storageKey)
    {
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, normalized));

        // Prevent path traversal through crafted storage keys.
        if (!absolutePath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The storage key points outside the configured root directory.");
        }

        return absolutePath;
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

    private static string ResolveRootPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
    }
}
