using System.Text.Json;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Domain.Inbox;
using CloudDocumentPipeline.Domain.Jobs;
using CloudDocumentPipeline.Domain.Outbox;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudDocumentPipeline.Worker;

// Replays job messages that were claimed by a worker but stayed in Processing too long.
public sealed class StaleInboxRecoveryWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private const string ConsumerName = "CloudDocumentPipeline.JobConsumer";
    private const int MaxRecoveryRetryCount = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<StaleInboxRecoveryWorker> _logger;

    public StaleInboxRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        RabbitMqSettings settings,
        ILogger<StaleInboxRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stale inbox recovery worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverStaleInboxesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while recovering stale inbox messages.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.StaleRecoveryScanSeconds), stoppingToken);
        }
    }

    private async Task RecoverStaleInboxesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Only job consumer inbox rows are recovered here; notification inbox rows are lightweight.
        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(-_settings.ProcessingTimeoutSeconds);

        var staleInboxIds = await dbContext.InboxMessages
            .Where(x =>
                x.ConsumerName == ConsumerName &&
                x.Status == InboxStatus.Processing &&
                x.ClaimedAtUtc <= staleBeforeUtc)
            .OrderBy(x => x.ClaimedAtUtc)
            .Select(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var inboxId in staleInboxIds)
        {
            await RecoverSingleInboxAsync(inboxId, cancellationToken);
        }
    }

    private async Task RecoverSingleInboxAsync(Guid inboxId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Keep the inbox, job, and replay outbox updates atomic.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(x => x.Id == inboxId, cancellationToken);
        if (inbox is null ||
            inbox.ConsumerName != ConsumerName ||
            !inbox.IsStale(DateTime.UtcNow, TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds)))
        {
            return;
        }

        var originalOutbox = await dbContext.OutboxMessages
            .Where(x =>
                x.Type == nameof(JobCreatedIntegrationMessage) &&
                x.PayloadJson.Contains(inbox.MessageId.ToString()))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (originalOutbox is null)
        {
            inbox.MarkFailed("Processing timed out, but the original integration message could not be found.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because its original message could not be resolved.",
                inbox.Id);
            return;
        }

        var originalMessage = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(originalOutbox.PayloadJson, JsonSerializerOptions);
        if (originalMessage is null || originalMessage.MessageId != inbox.MessageId)
        {
            inbox.MarkFailed("Processing timed out, but the original integration message payload was invalid.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because its original message payload was invalid.",
                inbox.Id);
            return;
        }

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == originalMessage.JobId, cancellationToken);
        if (job is null)
        {
            inbox.MarkFailed("Processing timed out, but the related job could not be found.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because job {JobId} no longer exists.",
                inbox.Id,
                originalMessage.JobId);
            return;
        }

        if (job.Status == JobStatus.Succeeded)
        {
            // The side effect finished, but the original consumer did not mark its inbox row.
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Stale inbox {InboxId} was completed because job {JobId} had already succeeded.",
                inbox.Id,
                job.Id);
            return;
        }

        var projectedRetryCount = job.Status switch
        {
            JobStatus.Pending or JobStatus.Processing => job.RetryCount + 1,
            JobStatus.Failed => job.RetryCount,
            _ => job.RetryCount
        };

        if (projectedRetryCount >= MaxRecoveryRetryCount)
        {
            // Cap recovery loops so a permanently broken job does not replay forever.
            if (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
            {
                job.MarkFailed("Processing timed out. Recovery retry limit reached.");
            }

            inbox.MarkFailed("Processing timed out. Recovery retry limit reached.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale recovery retry limit reached for job {JobId}. InboxId: {InboxId}, RetryCount: {RetryCount}, Limit: {Limit}",
                job.Id,
                inbox.Id,
                projectedRetryCount,
                MaxRecoveryRetryCount);
            return;
        }

        if (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
        {
            job.MarkFailed("Processing timed out. Recovery replay scheduled.");
            job.Retry();
        }
        else if (job.Status == JobStatus.Failed)
        {
            job.Retry();
        }

        inbox.MarkFailed("Processing timed out. Recovery replay scheduled.");

        // The outbox publisher will send this replay using the normal publishing path.
        var replayMessage = new JobCreatedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = job.Id,
            CorrelationId = originalMessage.CorrelationId,
            IdempotencyKey = originalMessage.IdempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        var replayPayload = JsonSerializer.Serialize(replayMessage, JsonSerializerOptions);
        dbContext.OutboxMessages.Add(new OutboxMessage(nameof(JobCreatedIntegrationMessage), replayPayload));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogWarning(
            "Recovered stale inbox {InboxId} for job {JobId}. Recovery replay scheduled. RecoveryRetryCount: {RetryCount}",
            inbox.Id,
            job.Id,
            projectedRetryCount);
    }
}
