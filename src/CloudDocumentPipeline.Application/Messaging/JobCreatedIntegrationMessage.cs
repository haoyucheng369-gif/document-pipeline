namespace CloudDocumentPipeline.Application.Messaging;

// Integration event published after a job is committed.
// Consumers use MessageId and IdempotencyKey for deduplication and replay safety.
public sealed class JobCreatedIntegrationMessage
{
    public Guid MessageId { get; set; }
    public Guid JobId { get; set; }
    public string CorrelationId { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}
