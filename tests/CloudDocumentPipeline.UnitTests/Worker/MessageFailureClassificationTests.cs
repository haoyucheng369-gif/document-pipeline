using System.Text.Json;
using CloudDocumentPipeline.Application.Exceptions;
using CloudDocumentPipeline.Worker;

namespace CloudDocumentPipeline.UnitTests.Worker;

public sealed class MessageFailureClassificationTests
{
    [Fact]
    public void Classify_JobNotFound_ReturnsDeadLetter()
    {
        var disposition = MessageFailureClassification.Classify(new JobNotFoundException(Guid.NewGuid()));

        Assert.Equal(MessageFailureDisposition.DeadLetter, disposition);
    }

    [Fact]
    public void Classify_InvalidPayload_ReturnsDeadLetter()
    {
        var disposition = MessageFailureClassification.Classify(new JsonException("bad json"));

        Assert.Equal(MessageFailureDisposition.DeadLetter, disposition);
    }

    [Fact]
    public void Classify_UnknownException_ReturnsRetry()
    {
        var disposition = MessageFailureClassification.Classify(new TimeoutException("transient"));

        Assert.Equal(MessageFailureDisposition.Retry, disposition);
    }
}
