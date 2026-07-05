namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Azure Service Bus topic and subscription settings used by cloud consumers.
public sealed class ServiceBusSettings
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public string TopicName { get; set; } = "job-events";
    public string WorkerSubscriptionName { get; set; } = "worker";
    public string NotificationSubscriptionName { get; set; } = "notification";
    public string ApiRealtimeSubscriptionName { get; set; } = "api-realtime";
}
