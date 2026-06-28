namespace CloudDocumentPipeline.Infrastructure.Storage;

// 瀛樺偍閰嶇疆锛?
// 褰撳墠榛樿浣跨敤 Local锛屽叡浜洰褰曢€傚悎鏈湴鑱旇皟锛?
// 鍚庣画涓婁簯鏃跺彲浠ユ妸 Provider 鍒囧埌 AzureBlob銆?
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
