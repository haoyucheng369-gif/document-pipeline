using CloudDocumentPipeline.Domain.Inbox;

namespace CloudDocumentPipeline.UnitTests.Inbox;

public sealed class InboxMessageTests
{
    [Fact]
    public void NewInboxMessage_StartsInProcessing()
    {
        var message = new InboxMessage(Guid.NewGuid(), "consumer");

        Assert.Equal(InboxStatus.Processing, message.Status);
        Assert.NotEqual(default, message.ClaimedAtUtc);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public void MarkProcessed_TransitionsToProcessed()
    {
        var message = new InboxMessage(Guid.NewGuid(), "consumer");

        message.MarkProcessed();

        Assert.Equal(InboxStatus.Processed, message.Status);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.ErrorMessage);
    }

    [Fact]
    public void MarkFailed_TransitionsToFailed()
    {
        var message = new InboxMessage(Guid.NewGuid(), "consumer");

        message.MarkFailed("boom");

        Assert.Equal(InboxStatus.Failed, message.Status);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Equal("boom", message.ErrorMessage);
    }

    [Fact]
    public void Reclaim_FromFailed_TransitionsBackToProcessing()
    {
        var message = new InboxMessage(Guid.NewGuid(), "consumer");
        message.MarkFailed("boom");

        message.Reclaim();

        Assert.Equal(InboxStatus.Processing, message.Status);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Null(message.ErrorMessage);
    }

    [Fact]
    public void MarkProcessed_FromProcessed_Throws()
    {
        var message = new InboxMessage(Guid.NewGuid(), "consumer");
        message.MarkProcessed();

        var exception = Assert.Throws<InvalidOperationException>(() => message.MarkProcessed());

        Assert.Equal("Only processing inbox messages can be marked as processed.", exception.Message);
    }
}
