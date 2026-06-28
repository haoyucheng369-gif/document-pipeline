using CloudDocumentPipeline.Domain.Jobs;

namespace CloudDocumentPipeline.UnitTests.Jobs;

public sealed class JobTests
{
    [Fact]
    public void Retry_DoesNotIncrementRetryCountAgain()
    {
        var job = new Job("demo", "pdf", "{}");

        job.MarkFailed("boom");
        job.Retry();

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.Null(job.ErrorMessage);
        Assert.Null(job.StartedAtUtc);
        Assert.Null(job.CompletedAtUtc);
    }

    [Fact]
    public void MarkSucceeded_FromPending_Throws()
    {
        var job = new Job("demo", "pdf", "{}");

        var exception = Assert.Throws<InvalidOperationException>(() => job.MarkSucceeded("{\"ok\":true}"));

        Assert.Equal("Only processing jobs can be marked as succeeded.", exception.Message);
    }

    [Fact]
    public void MarkProcessing_FromFailed_Throws()
    {
        var job = new Job("demo", "pdf", "{}");
        job.MarkFailed("boom");

        var exception = Assert.Throws<InvalidOperationException>(() => job.MarkProcessing());

        Assert.Equal("Only pending jobs can start processing.", exception.Message);
    }
}
