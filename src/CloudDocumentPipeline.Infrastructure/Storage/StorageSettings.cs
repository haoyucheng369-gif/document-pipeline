namespace CloudDocumentPipeline.Infrastructure.Storage;

// Storage provider settings. Development uses Local; cloud environments use AzureBlob.
public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";

    public LocalStorageSettings Local { get; set; } = new();

    public AzureBlobStorageSettings AzureBlob { get; set; } = new();
}

public sealed class LocalStorageSettings
{
    public string RootPath { get; set; } = "../../shared-storage";
}

public sealed class AzureBlobStorageSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "docflow-files";
}
