using CloudDocumentPipeline.Domain.Outbox;

namespace CloudDocumentPipeline.Application.Abstractions.Persistence;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<List<OutboxMessage>> GetUnprocessedAsync(int take, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}