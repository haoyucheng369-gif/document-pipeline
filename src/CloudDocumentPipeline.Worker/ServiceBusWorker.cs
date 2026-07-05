using System.Text.Json;
using Azure.Messaging.ServiceBus;
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
using Serilog.Context;

namespace CloudDocumentPipeline.Worker;

// Service Bus implementation of the conversion worker used by cloud environments.
public sealed class ServiceBusWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private const string ConsumerName = "CloudDocumentPipeline.JobConsumer";
    private const int MaxRetryCount = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSettings _settings;
    private readonly RabbitMqSettings _rabbitMqSettings;
    private readonly ILogger<ServiceBusWorker> _logger;

    private ServiceBusProcessor? _processor;

    public ServiceBusWorker(
        IServiceScopeFactory scopeFactory,
        ServiceBusClient serviceBusClient,
        ServiceBusSettings settings,
        RabbitMqSettings rabbitMqSettings,
        ILogger<ServiceBusWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _serviceBusClient = serviceBusClient;
        _settings = settings;
        _rabbitMqSettings = rabbitMqSettings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Worker has its own subscription so conversion processing is isolated from realtime and notification consumers.
        _processor = _serviceBusClient.CreateProcessor(
            _settings.TopicName,
            _settings.WorkerSubscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 4
            });

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        _logger.LogInformation(
            "Service Bus worker connected. Topic: {TopicName}, Subscription: {SubscriptionName}",
            _settings.TopicName,
            _settings.WorkerSubscriptionName);

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await _processor.StopProcessingAsync(CancellationToken.None);
            await _processor.DisposeAsync();
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var messageType = ResolveMessageType(args.Message);
        if (!string.Equals(messageType, nameof(JobCreatedIntegrationMessage), StringComparison.Ordinal))
        {
            // Ignore event types that are not meant for the conversion worker.
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        var json = args.Message.Body.ToString();

        try
        {
            var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions)
                ?? throw new InvalidOperationException("Message deserialization failed.");

            using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
            {
                using var scope = _scopeFactory.CreateScope();
                var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();

                // Keep the same inbox idempotency model as the RabbitMQ worker.
                var claimed = await inboxRepository.TryClaimAsync(
                    message.MessageId,
                    ConsumerName,
                    TimeSpan.FromSeconds(_rabbitMqSettings.ProcessingTimeoutSeconds),
                    args.CancellationToken);

                if (!claimed)
                {
                    _logger.LogWarning("Message was already claimed or processed. MessageId: {MessageId}", message.MessageId);
                    await args.CompleteMessageAsync(args.Message);
                    return;
                }

                var completion = await TryCompleteAlreadySucceededJobAsync(message, args.CancellationToken);
                if (completion)
                {
                    await args.CompleteMessageAsync(args.Message);
                    _logger.LogInformation("Job {JobId} was already succeeded. Side effects were skipped.", message.JobId);
                    return;
                }

                // PDF generation and storage are still provider-neutral application side effects.
                var resultJson = await ExecuteSideEffectsAsync(message, args.CancellationToken);

                await CompleteMessageAsync(message, resultJson, args.CancellationToken);

                await args.CompleteMessageAsync(args.Message);
                _logger.LogInformation("Job {JobId} processed successfully.", message.JobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing Service Bus message.");
            await HandleFailureAsync(args, json, ex);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus worker error. ErrorSource: {ErrorSource}, EntityPath: {EntityPath}",
            args.ErrorSource,
            args.EntityPath);

        return Task.CompletedTask;
    }

    private static string ResolveMessageType(ServiceBusReceivedMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Subject))
        {
            return message.Subject;
        }

        if (message.ApplicationProperties.TryGetValue("messageType", out var value) && value is string text)
        {
            return text;
        }

        return string.Empty;
    }

    private async Task<bool> TryCompleteAlreadySucceededJobAsync(
        JobCreatedIntegrationMessage message,
        CancellationToken cancellationToken)
    {
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
        ProcessMessageEventArgs args,
        string json,
        Exception exception)
    {
        await TryMarkFailedAsync(json, exception, args.CancellationToken);

        var disposition = MessageFailureClassification.Classify(exception);
        var deliveryCount = args.Message.DeliveryCount;

        // Service Bus handles retry through abandon/redelivery and terminal failures through DLQ.
        if (disposition == MessageFailureDisposition.Retry && deliveryCount < MaxRetryCount)
        {
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
            _logger.LogWarning(
                "Retryable error. Service Bus message abandoned for retry. DeliveryCount: {DeliveryCount}",
                deliveryCount);
        }
        else
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: disposition.ToString(),
                deadLetterErrorDescription: exception.Message,
                cancellationToken: args.CancellationToken);

            _logger.LogError(
                "Message moved to Service Bus DLQ. Disposition: {Disposition}, DeliveryCount: {DeliveryCount}",
                disposition,
                deliveryCount);
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
        // Status changes return to the configured broker so API realtime consumers can broadcast them.
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
}
