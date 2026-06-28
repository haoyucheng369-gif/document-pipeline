namespace CloudDocumentPipeline.Application.Messaging;

// Job 鍒涘缓浜嬩欢锛?
// 杩欐槸鏈嶅姟闂撮€氳繃 RabbitMQ 浼犻€掔殑闆嗘垚娑堟伅濂戠害銆?
// 瀹冧笉鏄?HTTP DTO锛岃€屾槸鈥淛ob 宸插垱寤猴紝鍙互寮€濮嬪悗鍙板鐞嗕簡鈥濊繖浠朵簨鐨勪簨浠惰浇浣撱€?
public sealed class JobCreatedIntegrationMessage
{
    public Guid MessageId { get; set; }
    public Guid JobId { get; set; }
    public string CorrelationId { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}
