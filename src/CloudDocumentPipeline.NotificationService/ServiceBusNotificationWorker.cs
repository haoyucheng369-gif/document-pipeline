using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Domain.Inbox;
using CloudDocumentPipeline.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace CloudDocumentPipeline.NotificationService;

// Service Bus 鐗堥€氱煡娑堣垂鑰咃細
// 浠?job-events / notification subscription 璇诲彇 JobCreatedIntegrationMessage锛?
// 鍐嶆部鐢ㄧ幇鏈?Inbox 鍘婚噸鍜屾ā鎷熼€氱煡鍙戦€侀€昏緫銆?
public sealed class ServiceBusNotificationWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private const string ConsumerName = "CloudDocumentPipeline.NotificationConsumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSettings _settings;
    private readonly RabbitMqSettings _rabbitMqSettings;
    private readonly ILogger<ServiceBusNotificationWorker> _logger;

    private ServiceBusProcessor? _processor;

    public ServiceBusNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ServiceBusClient serviceBusClient,
        ServiceBusSettings settings,
        RabbitMqSettings rabbitMqSettings,
        ILogger<ServiceBusNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _serviceBusClient = serviceBusClient;
        _settings = settings;
        _rabbitMqSettings = rabbitMqSettings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _serviceBusClient.CreateProcessor(
            _settings.TopicName,
            _settings.NotificationSubscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 4
            });

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        _logger.LogInformation(
            "Service Bus notification worker connected. Topic: {TopicName}, Subscription: {SubscriptionName}",
            _settings.TopicName,
            _settings.NotificationSubscriptionName);

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
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        var json = args.Message.Body.ToString();

        try
        {
            var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions)
                ?? throw new InvalidOperationException("Notification message deserialization failed.");

            using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
            {
                using var activity = CloudDocumentPipelineTracing.ActivitySource.StartActivity("notification.process-message", ActivityKind.Consumer);
                activity?.SetTag("job.id", message.JobId);
                activity?.SetTag("message.id", message.MessageId);
                activity?.SetTag("message.type", nameof(JobCreatedIntegrationMessage));
                activity?.SetTag("correlation.id", message.CorrelationId);

                using var scope = _scopeFactory.CreateScope();
                var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();
                var sender = scope.ServiceProvider.GetRequiredService<NotificationEmailSender>();

                var claimed = await inboxRepository.TryClaimAsync(
                    message.MessageId,
                    ConsumerName,
                    TimeSpan.FromSeconds(_rabbitMqSettings.ProcessingTimeoutSeconds),
                    args.CancellationToken);

                if (!claimed)
                {
                    _logger.LogWarning(
                        "Notification message already claimed or processed. MessageId: {MessageId}",
                        message.MessageId);
                    await args.CompleteMessageAsync(args.Message);
                    return;
                }

                var inbox = await inboxRepository.GetByMessageIdAsync(message.MessageId, ConsumerName, args.CancellationToken)
                    ?? throw new InvalidOperationException($"Inbox claim for notification message '{message.MessageId}' was not found.");

                await sender.SendAsync(message, args.CancellationToken);

                inbox.MarkProcessed();
                await inboxRepository.SaveChangesAsync(args.CancellationToken);

                await args.CompleteMessageAsync(args.Message);
                _logger.LogInformation("Notification sent for job {JobId}.", message.JobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing Service Bus notification message.");
            await args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: "NotificationConsumerFailed",
                deadLetterErrorDescription: ex.Message,
                cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus notification worker error. ErrorSource: {ErrorSource}, EntityPath: {EntityPath}",
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
}
