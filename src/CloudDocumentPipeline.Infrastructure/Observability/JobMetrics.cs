using System.Diagnostics.Metrics;
using CloudDocumentPipeline.Application.Abstractions.Observability;

namespace CloudDocumentPipeline.Infrastructure.Observability;

public sealed class JobMetrics : IJobMetrics
{
    public const string MeterName = "CloudDocumentPipeline.Jobs";

    private static readonly Meter Meter = new(MeterName);

    private readonly Counter<long> _jobsCreatedCounter = Meter.CreateCounter<long>(
        "jobs_created_total",
        unit: "{job}",
        description: "Total number of jobs created.");

    private readonly Counter<long> _jobsFailedCounter = Meter.CreateCounter<long>(
        "jobs_failed_total",
        unit: "{job}",
        description: "Total number of jobs failed.");

    private readonly Counter<long> _jobsRetriedCounter = Meter.CreateCounter<long>(
        "jobs_retried_total",
        unit: "{job}",
        description: "Total number of business-level job retries.");

    private readonly Counter<long> _jobsSucceededCounter = Meter.CreateCounter<long>(
        "jobs_succeeded_total",
        unit: "{job}",
        description: "Total number of jobs succeeded.");

    private readonly Histogram<double> _jobProcessingDuration = Meter.CreateHistogram<double>(
        "job_processing_duration_seconds",
        unit: "s",
        description: "Job processing duration in seconds.");

    public void JobCreated(string jobType)
    {
        _jobsCreatedCounter.Add(1, new KeyValuePair<string, object?>("job.type", jobType));
    }

    public void JobFailed(string jobType)
    {
        _jobsFailedCounter.Add(1, new KeyValuePair<string, object?>("job.type", jobType));
    }

    public void JobRetried(string jobType)
    {
        _jobsRetriedCounter.Add(1, new KeyValuePair<string, object?>("job.type", jobType));
    }

    public void JobSucceeded(string jobType)
    {
        _jobsSucceededCounter.Add(1, new KeyValuePair<string, object?>("job.type", jobType));
    }

    public void RecordProcessingDuration(string jobType, double durationSeconds)
    {
        _jobProcessingDuration.Record(durationSeconds, new KeyValuePair<string, object?>("job.type", jobType));
    }
}
