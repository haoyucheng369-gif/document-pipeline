namespace CloudDocumentPipeline.Application.Abstractions.Observability;

public interface IJobMetrics
{
    void JobCreated(string jobType);

    void JobFailed(string jobType);

    void JobRetried(string jobType);

    void JobSucceeded(string jobType);

    void RecordProcessingDuration(string jobType, double durationSeconds);
}
