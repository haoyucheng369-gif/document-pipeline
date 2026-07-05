using System.Text;
using System.Text.Json;
using CloudDocumentPipeline.Application.Abstractions.Messaging;
using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Application.Abstractions.Processing;
using CloudDocumentPipeline.Application.Exceptions;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Domain.Inbox;
using CloudDocumentPipeline.Domain.Jobs;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace CloudDocumentPipeline.Worker;

public sealed class RabbitMqWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    // Inbox deduplication is scoped by message ID and consumer name.
    private const string ConsumerName = "CloudDocumentPipeline.JobConsumer";
    // RabbitMQ retries are explicit delayed republishes before the message moves to the DLQ.
    private const int MaxRetryCount = 3;
    private static readonly int[] RetryDelaysInSeconds = [1, 5, 30];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqWorker> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings,
        ILogger<RabbitMqWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Keep the connection setup separate from message handling so startup failures are visible.
        _connection = _connectionProvider.GetConnection();
        _channel = _connection.CreateModel();
        _topologyInitializer.EnsureTopology(_channel);

        // Limit unacknowledged deliveries per worker instance to avoid overloading storage and SQL.
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _logger.LogInformation("RabbitMQ worker connected. Queue: {QueueName}", _settings.QueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            _logger.LogInformation("Received message: {Message}", json);

            try
            {
                // RabbitMQ carries the raw integration contract serialized by the outbox publisher.
                var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Message deserialization failed.");

                using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();

                    // Claim before side effects so duplicate deliveries do not generate duplicate PDFs.
                    var claimed = await inboxRepository.TryClaimAsync(
                        message.MessageId,
                        ConsumerName,
                        TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds),
                        stoppingToken);

                    if (!claimed)
                    {
                        _logger.LogWarning("Message was already claimed or processed. MessageId: {MessageId}", message.MessageId);
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        return;
                    }

                    var completion = await TryCompleteAlreadySucceededJobAsync(message, stoppingToken);
                    if (completion)
                    {
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        _logger.LogInformation("Job {JobId} was already succeeded. Side effects were skipped.", message.JobId);
                        return;
                    }

                    var resultJson = await ExecuteSideEffectsAsync(message, stoppingToken);

                    // Job and inbox state are committed together after the external side effect succeeds.
                    await CompleteMessageAsync(message, resultJson, stoppingToken);

                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    _logger.LogInformation("Job {JobId} processed successfully.", message.JobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing message.");

                await HandleFailureAsync(json, ex, body, eventArgs.BasicProperties, stoppingToken);
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("RabbitMQ consumer started.");

        return Task.CompletedTask;
    }

    private async Task<bool> TryCompleteAlreadySucceededJobAsync(
        JobCreatedIntegrationMessage message,
        CancellationToken cancellationToken)
    {
        // A replay can arrive after an earlier attempt already completed the job.
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        if (job.Status != JobStatus.Succeeded)
        {
            return false;
        }

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
            x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
            cancellationToken)
            ?? throw new InvalidOperationException($"Inbox claim for message '{message.MessageId}' was not found.");

        if (inbox.Status == InboxStatus.Processing)
        {
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return true;
    }

    private async Task<string> ExecuteSideEffectsAsync(
        JobCreatedIntegrationMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jobMetrics = scope.ServiceProvider.GetRequiredService<IJobMetrics>();
        var executor = scope.ServiceProvider.GetRequiredService<IJobSideEffectExecutor>();

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        return await executor.ExecuteAsync(
            job.Id,
            job.Type,
            job.PayloadJson,
            message.IdempotencyKey,
            message.CorrelationId,
            cancellationToken);
    }

    private async Task CompleteMessageAsync(
        JobCreatedIntegrationMessage message,
        string resultJson,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jobMetrics = scope.ServiceProvider.GetRequiredService<IJobMetrics>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
            x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
            cancellationToken)
            ?? throw new InvalidOperationException($"Inbox claim for message '{message.MessageId}' was not found.");

        if (job.Status == JobStatus.Succeeded)
        {
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        job.MarkProcessing();
        job.MarkSucceeded(resultJson);
        inbox.MarkProcessed();

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        jobMetrics.JobSucceeded(job.Type);
        RecordProcessingDuration(job, jobMetrics);

        await PublishJobStatusChangedAsync(
            message.JobId,
            JobStatus.Succeeded,
            job.RetryCount,
            message.CorrelationId,
            cancellationToken);
    }

    private async Task HandleFailureAsync(
        string json,
        Exception exception,
        byte[] body,
        IBasicProperties? properties,
        CancellationToken cancellationToken)
    {
        // Record the business failure before deciding whether broker-level retry is still allowed.
        await TryMarkFailedAsync(json, exception, cancellationToken);

        try
        {
            var disposition = MessageFailureClassification.Classify(exception);
            var retryCount = GetRetryCount(properties);

            if (disposition == MessageFailureDisposition.Retry && retryCount < MaxRetryCount)
            {
                // The retry queue uses TTL plus dead-letter routing to return messages later.
                RepublishWithRetry(body, retryCount + 1);
                _logger.LogWarning(
                    "Retryable error. Message scheduled for retry {RetryCount} after {DelaySeconds}s",
                    retryCount + 1,
                    GetRetryDelaySeconds(retryCount + 1));
            }
            else
            {
                PublishToDeadLetter(body);
                _logger.LogError(
                    "Message moved to DLQ. Disposition: {Disposition}, RetryCount: {RetryCount}",
                    disposition,
                    retryCount);
            }
        }
        catch (Exception retryEx)
        {
            _logger.LogError(retryEx, "Failed during retry/DLQ handling.");
        }
    }

    private async Task TryMarkFailedAsync(string json, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions);
            if (message is null)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jobMetrics = scope.ServiceProvider.GetRequiredService<IJobMetrics>();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken);
            var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
                x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
                cancellationToken);

            if (job is not null && job.Status != JobStatus.Succeeded && job.Status != JobStatus.Failed)
            {
                job.MarkFailed(exception.Message);
            }

            if (inbox is not null && inbox.Status == InboxStatus.Processing)
            {
                inbox.MarkFailed(exception.Message);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (job is not null && job.Status == JobStatus.Failed)
            {
                jobMetrics.JobFailed(job.Type);
                RecordProcessingDuration(job, jobMetrics);
                await PublishJobStatusChangedAsync(
                    job.Id,
                    JobStatus.Failed,
                    job.RetryCount,
                    message.CorrelationId,
                    cancellationToken);
            }
        }
        catch (Exception markFailedException)
        {
            _logger.LogError(markFailedException, "Failed to mark job and inbox as failed after processing error.");
        }
    }

    private async Task PublishJobStatusChangedAsync(
        Guid jobId,
        JobStatus status,
        int retryCount,
        string correlationId,
        CancellationToken cancellationToken)
    {
        // Status changes are published through the configured broker so API realtime consumers can fan out.
        using var scope = _scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IJobMessagePublisher>();

        var message = new JobStatusChangedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = jobId,
            Status = status.ToString(),
            RetryCount = retryCount,
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        };

        var payloadJson = JsonSerializer.Serialize(message, JsonSerializerOptions);

        await publisher.PublishRawAsync(
            nameof(JobStatusChangedIntegrationMessage),
            payloadJson,
            cancellationToken);
    }

    private int GetRetryCount(IBasicProperties? properties)
    {
        if (properties?.Headers is null)
            return 0;

        if (!properties.Headers.TryGetValue("x-retry-count", out var value))
            return 0;

        if (value is byte[] bytes && int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed))
            return parsed;

        if (value is int intValue)
            return intValue;

        return 0;
    }

    private void RepublishWithRetry(byte[] body, int retryCount)
    {
        if (_channel is null)
            return;

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Expiration = TimeSpan.FromSeconds(GetRetryDelaySeconds(retryCount)).TotalMilliseconds.ToString("F0");
        properties.Headers = new Dictionary<string, object>
        {
            ["x-retry-count"] = retryCount.ToString()
        };

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.RetryQueueName,
            basicProperties: properties,
            body: body);
    }

    private static int GetRetryDelaySeconds(int retryCount)
    {
        var index = Math.Clamp(retryCount - 1, 0, RetryDelaysInSeconds.Length - 1);
        return RetryDelaysInSeconds[index];
    }

    private void PublishToDeadLetter(byte[] body)
    {
        if (_channel is null)
            return;

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.DeadLetterQueueName,
            basicProperties: properties,
            body: body);
    }

    private static void RecordProcessingDuration(Job job, IJobMetrics jobMetrics)
    {
        if (job.StartedAtUtc is null || job.CompletedAtUtc is null)
        {
            return;
        }

        var durationSeconds = (job.CompletedAtUtc.Value - job.StartedAtUtc.Value).TotalSeconds;
        if (durationSeconds >= 0)
        {
            jobMetrics.RecordProcessingDuration(job.Type, durationSeconds);
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
