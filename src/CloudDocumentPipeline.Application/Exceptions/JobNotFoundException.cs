namespace CloudDocumentPipeline.Application.Exceptions;

public sealed class JobNotFoundException : Exception
{
    public JobNotFoundException(Guid jobId)
        : base($"Job '{jobId}' was not found.")
    {
        JobId = jobId;
    }

    public Guid JobId { get; }
}
