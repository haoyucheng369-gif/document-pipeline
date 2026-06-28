namespace CloudDocumentPipeline.Application.Messaging;

// Job 鐘舵€佸彉鍖栦簨浠讹細
// 鐢?Worker 鍦ㄤ换鍔℃垚鍔熸垨澶辫触鍚庡彂甯冿紝渚?API 瀹炴椂灞傝闃呭苟杞垚 SignalR 鎺ㄩ€併€?
public sealed class JobStatusChangedIntegrationMessage
{
    public Guid MessageId { get; set; }
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
