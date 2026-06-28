using CloudDocumentPipeline.Infrastructure.Persistence;
using CloudDocumentPipeline.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CloudDocumentPipeline.UnitTests.Inbox;

public sealed class InboxMessageRepositoryTests
{
    [Fact]
    public async Task TryClaimAsync_ReturnsFalse_WhenSameMessageAlreadyClaimed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE InboxMessages (
                    Id TEXT NOT NULL PRIMARY KEY,
                    MessageId TEXT NOT NULL,
                    ConsumerName TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    ClaimedAtUtc TEXT NOT NULL,
                    ProcessedAtUtc TEXT NULL,
                    ErrorMessage TEXT NULL
                );

                CREATE UNIQUE INDEX IX_InboxMessages_MessageId_ConsumerName
                ON InboxMessages (MessageId, ConsumerName);
                """;

            await command.ExecuteNonQueryAsync();
        }

        await using var firstContext = new AppDbContext(options);
        var firstRepository = new InboxMessageRepository(firstContext);
        var messageId = Guid.NewGuid();

        var firstClaim = await firstRepository.TryClaimAsync(messageId, "consumer", TimeSpan.FromMinutes(5));

        await using var secondContext = new AppDbContext(options);
        var secondRepository = new InboxMessageRepository(secondContext);
        var secondClaim = await secondRepository.TryClaimAsync(messageId, "consumer", TimeSpan.FromMinutes(5));

        Assert.True(firstClaim);
        Assert.False(secondClaim);
    }

    [Fact]
    public async Task TryClaimAsync_ReturnsTrue_WhenExistingMessageIsFailed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE InboxMessages (
                    Id TEXT NOT NULL PRIMARY KEY,
                    MessageId TEXT NOT NULL,
                    ConsumerName TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    ClaimedAtUtc TEXT NOT NULL,
                    ProcessedAtUtc TEXT NULL,
                    ErrorMessage TEXT NULL
                );

                CREATE UNIQUE INDEX IX_InboxMessages_MessageId_ConsumerName
                ON InboxMessages (MessageId, ConsumerName);
                """;

            await command.ExecuteNonQueryAsync();
        }

        var messageId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.InboxMessages.Add(new CloudDocumentPipeline.Domain.Inbox.InboxMessage(messageId, "consumer"));
            await seedContext.SaveChangesAsync();

            var seeded = await seedContext.InboxMessages.SingleAsync();
            seeded.MarkFailed("boom");
            await seedContext.SaveChangesAsync();
        }

        await using var reclaimContext = new AppDbContext(options);
        var repository = new InboxMessageRepository(reclaimContext);

        var claimResult = await repository.TryClaimAsync(messageId, "consumer", TimeSpan.FromMinutes(5));

        Assert.True(claimResult);
    }

    [Fact]
    public async Task TryClaimAsync_ReturnsTrue_WhenProcessingClaimIsStale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE InboxMessages (
                    Id TEXT NOT NULL PRIMARY KEY,
                    MessageId TEXT NOT NULL,
                    ConsumerName TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    ClaimedAtUtc TEXT NOT NULL,
                    ProcessedAtUtc TEXT NULL,
                    ErrorMessage TEXT NULL
                );

                CREATE UNIQUE INDEX IX_InboxMessages_MessageId_ConsumerName
                ON InboxMessages (MessageId, ConsumerName);
                """;

            await command.ExecuteNonQueryAsync();
        }

        var messageId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.InboxMessages.Add(new CloudDocumentPipeline.Domain.Inbox.InboxMessage(messageId, "consumer"));
            await seedContext.SaveChangesAsync();
        }

        await using (var staleCommand = connection.CreateCommand())
        {
            staleCommand.CommandText = """
                UPDATE InboxMessages
                SET ClaimedAtUtc = @claimedAtUtc
                WHERE MessageId = @messageId AND ConsumerName = 'consumer'
                """;
            staleCommand.Parameters.AddWithValue("@claimedAtUtc", DateTime.UtcNow.AddMinutes(-10));
            staleCommand.Parameters.AddWithValue("@messageId", messageId);
            await staleCommand.ExecuteNonQueryAsync();
        }

        await using var reclaimContext = new AppDbContext(options);
        var repository = new InboxMessageRepository(reclaimContext);

        var claimResult = await repository.TryClaimAsync(messageId, "consumer", TimeSpan.FromMinutes(5));

        Assert.True(claimResult);
    }
}
