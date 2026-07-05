using CloudDocumentPipeline.Domain.Inbox;

namespace CloudDocumentPipeline.Application.Abstractions.Persistence;

public interface IInboxMessageRepository
{
    Task<bool> TryClaimAsync(Guid messageId, string consumerName, TimeSpan processingTimeout, CancellationToken cancellationToken = default);

    Task<InboxMessage?> GetByMessageIdAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
