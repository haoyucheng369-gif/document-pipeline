namespace CloudDocumentPipeline.Domain.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string type, string payloadJson)
    {
        Id = Guid.NewGuid();
        Type = type;
        PayloadJson = payloadJson;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProcessed()
    {
        ProcessedAtUtc = DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        ErrorMessage = errorMessage;
        RetryCount++;
    }
}