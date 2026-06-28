namespace CloudDocumentPipeline.Application.Jobs;

// DocumentToPdf 浠诲姟杈撳嚭锛?
// 杞崲缁撴灉鍚屾牱鍙繚瀛樿緭鍑烘枃浠剁殑 storage key锛屽簳灞傜墿鐞嗕綅缃敱瀛樺偍瀹炵幇璐熻矗鏄犲皠銆?
public sealed class DocumentToPdfJobResult
{
    public string OutputFileName { get; set; } = default!;
    public string OutputStorageKey { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
}
