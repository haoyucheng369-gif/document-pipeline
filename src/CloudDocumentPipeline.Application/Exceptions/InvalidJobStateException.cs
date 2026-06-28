namespace CloudDocumentPipeline.Application.Exceptions;

public sealed class InvalidJobStateException : Exception
{
    public InvalidJobStateException(Guid jobId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        JobId = jobId;
    }

    public Guid JobId { get; }
}
