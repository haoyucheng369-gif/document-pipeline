namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Messaging provider switch. Development defaults to RabbitMQ; cloud can use ServiceBus.
public sealed class MessagingSettings
{
    public const string SectionName = "Messaging";

    public string Provider { get; set; } = "RabbitMq";
}
