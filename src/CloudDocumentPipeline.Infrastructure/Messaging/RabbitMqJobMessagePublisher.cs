using System.Text;
using CloudDocumentPipeline.Application.Abstractions.Messaging;
using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// RabbitMQ publisher used by the outbox worker in local development.
public sealed class RabbitMqJobMessagePublisher : IJobMessagePublisher
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;

    public RabbitMqJobMessagePublisher(
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings)
    {
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
    }

    public Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // The outbox already stores serialized integration messages, so raw publishing is required.
        throw new NotSupportedException("Use PublishRawAsync for integration messages.");
    }

    public Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default)
    {
        // Channels are cheap and not thread-safe; create one per publish operation.
        using var channel = _connectionProvider.GetConnection().CreateModel();

        _topologyInitializer.EnsureTopology(channel);

        var body = Encoding.UTF8.GetBytes(payloadJson);
        // Persistent messages survive broker restarts when the queues are durable.
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        channel.BasicPublish(
            exchange: _settings.TopicExchangeName,
            routingKey: ResolveRoutingKey(messageType),
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    private string ResolveRoutingKey(string messageType)
    {
        // Routing is based on the integration contract name stored in the outbox row.
        return messageType switch
        {
            nameof(Application.Messaging.JobCreatedIntegrationMessage) => _settings.JobCreatedRoutingKey,
            nameof(Application.Messaging.JobStatusChangedIntegrationMessage) => _settings.JobStatusChangedRoutingKey,
            _ => throw new NotSupportedException($"Unsupported integration message type '{messageType}'.")
        };
    }
}
