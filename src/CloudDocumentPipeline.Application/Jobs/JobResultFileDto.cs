namespace CloudDocumentPipeline.Application.Jobs;

// 缁撴灉鏂囦欢涓嬭浇 DTO锛?
// API 灞傜敤瀹冩妸浠诲姟缁撴灉杞崲鎴?File 鍝嶅簲銆?
public sealed class JobResultFileDto
{
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
}
