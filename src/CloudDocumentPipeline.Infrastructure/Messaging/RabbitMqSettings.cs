namespace CloudDocumentPipeline.Infrastructure.Messaging;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    public string TopicExchangeName { get; set; } = "docflow.events";
    public string QueueName { get; set; } = "docflow.jobs";
    public string NotificationQueueName { get; set; } = "docflow.notifications";
    public string RetryQueueName { get; set; } = "docflow.jobs.retry";
    public string DeadLetterQueueName { get; set; } = "docflow.jobs.dlq";
    public string JobStatusUpdatesQueueName { get; set; } = "docflow.job-status-updates";
    public string JobCreatedRoutingKey { get; set; } = "job.created";
    public string JobStatusChangedRoutingKey { get; set; } = "job.status.changed";
    public string JobQueueBindingKey { get; set; } = "job.created";
    public string NotificationQueueBindingKey { get; set; } = "job.*";
    public string JobStatusUpdatesBindingKey { get; set; } = "job.status.changed";
    public int ProcessingTimeoutSeconds { get; set; } = 300;
    public int StaleRecoveryScanSeconds { get; set; } = 30;
}
