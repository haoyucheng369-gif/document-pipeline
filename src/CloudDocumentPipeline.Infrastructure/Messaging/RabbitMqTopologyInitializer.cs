using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Declares the RabbitMQ exchanges, queues, bindings, retry queue, and DLQ used locally.
public sealed class RabbitMqTopologyInitializer : IRabbitMqTopologyInitializer
{
    private readonly RabbitMqSettings _settings;

    public RabbitMqTopologyInitializer(RabbitMqSettings settings)
    {
        _settings = settings;
    }

    public void EnsureTopology(IModel channel)
    {
        // A topic exchange lets different event types fan out to worker, notification, and realtime queues.
        channel.ExchangeDeclare(
            exchange: _settings.TopicExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        // Dead-letter queue keeps messages that exhausted retry or cannot be processed.
        channel.QueueDeclare(
            queue: _settings.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        // Retry queue returns messages to the job-created route after their per-message TTL expires.
        var retryQueueArguments = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = _settings.TopicExchangeName,
            ["x-dead-letter-routing-key"] = _settings.JobCreatedRoutingKey
        };

        channel.QueueDeclare(
            queue: _settings.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryQueueArguments);

        // Main worker queue dead-letters terminal failures into the local DLQ.
        var mainQueueArguments = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = _settings.DeadLetterQueueName
        };

        // Notification has its own queue so secondary side effects do not block job processing.
        channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments);
        channel.QueueBind(_settings.QueueName, _settings.TopicExchangeName, _settings.JobQueueBindingKey);

        // API realtime has its own queue so SignalR fan-out is independent from workers.
        channel.QueueDeclare(
            queue: _settings.NotificationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(_settings.NotificationQueueName, _settings.TopicExchangeName, _settings.NotificationQueueBindingKey);

        channel.QueueDeclare(
            queue: _settings.JobStatusUpdatesQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(
            _settings.JobStatusUpdatesQueueName,
            _settings.TopicExchangeName,
            _settings.JobStatusUpdatesBindingKey);
    }
}
