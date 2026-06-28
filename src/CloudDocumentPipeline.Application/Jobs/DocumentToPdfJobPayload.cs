namespace CloudDocumentPipeline.Application.Jobs;

// DocumentToPdf 浠诲姟杈撳叆锛?
// 鏁版嵁搴撻噷鍙繚瀛樿緭鍏ユ枃浠剁殑 storage key锛岃€屼笉鏄墿鐞嗙粷瀵硅矾寰勩€?
public sealed class DocumentToPdfJobPayload
{
    public string OriginalFileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string InputStorageKey { get; set; } = default!;
}
