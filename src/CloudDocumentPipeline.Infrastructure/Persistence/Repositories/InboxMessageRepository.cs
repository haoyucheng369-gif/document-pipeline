using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Domain.Inbox;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CloudDocumentPipeline.Infrastructure.Persistence.Repositories;

// Provides idempotent message claiming for consumers.
public sealed class InboxMessageRepository : IInboxMessageRepository
{
    private readonly AppDbContext _dbContext;

    public InboxMessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryClaimAsync(
        Guid messageId,
        string consumerName,
        TimeSpan processingTimeout,
        CancellationToken cancellationToken = default)
    {
        // Prefer insert-first claiming so the database unique key arbitrates concurrent consumers.
        var inboxMessage = new InboxMessage(messageId, consumerName);
        await _dbContext.InboxMessages.AddAsync(inboxMessage, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.Entry(inboxMessage).State = EntityState.Detached;

            var existingInbox = await GetByMessageIdAsync(messageId, consumerName, cancellationToken);
            if (existingInbox is null)
            {
                return false;
            }

            if (existingInbox.Status == InboxStatus.Processed)
            {
                // A completed message must never run its side effects again.
                return false;
            }

            if (existingInbox.Status == InboxStatus.Failed ||
                existingInbox.IsStale(DateTime.UtcNow, processingTimeout))
            {
                // Failed or stale work can be reclaimed by another worker instance.
                existingInbox.Reclaim();
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            return false;
        }
    }

    public async Task<InboxMessage?> GetByMessageIdAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InboxMessages
            .FirstOrDefaultAsync(
                x => x.MessageId == messageId && x.ConsumerName == consumerName,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        // Test and production providers expose unique-key failures through different exception types.
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        if (exception.InnerException is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode == 19;
        }

        return false;
    }
}
