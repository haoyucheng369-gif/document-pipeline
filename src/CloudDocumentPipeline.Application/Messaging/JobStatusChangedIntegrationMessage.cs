namespace CloudDocumentPipeline.Application.Messaging;

// Integration event published by workers after a job reaches a visible status change.
public sealed class JobStatusChangedIntegrationMessage
{
    public Guid MessageId { get; set; }
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
