using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CloudDocumentPipeline.Infrastructure.Persistence.Repositories;

// Repository for persisted integration messages waiting to be published.
public sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly AppDbContext _dbContext;

    public OutboxMessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        // Adds a message to the same DbContext transaction as the business change.
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<List<OutboxMessage>> GetUnprocessedAsync(int take, CancellationToken cancellationToken = default)
    {
        // Publish oldest messages first to preserve approximate event order.
        return await _dbContext.OutboxMessages
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Persists processed/failed markers written by the outbox publisher.
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
