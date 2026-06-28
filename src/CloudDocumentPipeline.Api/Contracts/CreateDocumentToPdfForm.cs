namespace CloudDocumentPipeline.Api.Contracts;

// 鏂囨。杞?PDF 鐨勮〃鍗曡姹傛ā鍨嬨€?
// 鐢ㄧ嫭绔嬫ā鍨嬫壙杞?IFormFile锛孲wagger/OpenAPI 瀵?multipart/form-data 鐨勭敓鎴愭洿绋冲畾銆?
public sealed class CreateDocumentToPdfForm
{
    public IFormFile File { get; set; } = default!;
    public string? Name { get; set; }
}
