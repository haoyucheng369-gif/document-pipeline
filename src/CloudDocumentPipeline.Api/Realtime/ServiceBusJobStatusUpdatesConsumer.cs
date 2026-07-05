using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace CloudDocumentPipeline.Api.Realtime;

// Service Bus-backed realtime bridge from the api-realtime subscription to SignalR clients.
public sealed class ServiceBusJobStatusUpdatesConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSettings _settings;
    private readonly IHubContext<JobUpdatesHub> _hubContext;
    private readonly ILogger<ServiceBusJobStatusUpdatesConsumer> _logger;

    private ServiceBusProcessor? _processor;

    public ServiceBusJobStatusUpdatesConsumer(
        ServiceBusClient serviceBusClient,
        ServiceBusSettings settings,
        IHubContext<JobUpdatesHub> hubContext,
        ILogger<ServiceBusJobStatusUpdatesConsumer> logger)
    {
        _serviceBusClient = serviceBusClient;
        _settings = settings;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The API has its own subscription so realtime fan-out is isolated from worker processing.
        _processor = _serviceBusClient.CreateProcessor(
            _settings.TopicName,
            _settings.ApiRealtimeSubscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 4
            });

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        _logger.LogInformation(
            "Service Bus realtime consumer connected. Topic: {TopicName}, Subscription: {SubscriptionName}",
            _settings.TopicName,
            _settings.ApiRealtimeSubscriptionName);

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
        if (!string.Equals(messageType, nameof(JobStatusChangedIntegrationMessage), StringComparison.Ordinal))
        {
            // Ignore unrelated event types on this subscription rather than poison the processor.
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        var json = args.Message.Body.ToString();

        try
        {
            // Convert the broker event into the compact SignalR payload expected by the UI.
            var message = JsonSerializer.Deserialize<JobStatusChangedIntegrationMessage>(json, JsonSerializerOptions)
                ?? throw new InvalidOperationException("Status change message deserialization failed.");

            await _hubContext.Clients.All.SendAsync(
                "jobUpdated",
                new
                {
                    jobId = message.JobId,
                    status = message.Status,
                    retryCount = message.RetryCount
                },
                args.CancellationToken);

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception exception)
        {
            // Service Bus keeps bad realtime messages in DLQ for later inspection.
            _logger.LogError(exception, "Failed to process Service Bus job status update message: {Message}", json);
            await args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: "RealtimeConsumerFailed",
                deadLetterErrorDescription: exception.Message,
                cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus realtime consumer error. ErrorSource: {ErrorSource}, EntityPath: {EntityPath}",
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
