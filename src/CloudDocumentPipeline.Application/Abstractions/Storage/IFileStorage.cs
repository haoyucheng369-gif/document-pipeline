namespace CloudDocumentPipeline.Application.Abstractions.Storage;

// 鏂囦欢瀛樺偍鎶借薄锛?
// 涓氬姟灞傚彧鍏冲績 storage key锛屼笉鍏冲績搴曞眰鏄湰鍦扮洰褰曘€丯AS 杩樻槸 Azure Blob銆?
public interface IFileStorage
{
    Task<string> SaveAsync(
        string category,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);
}
