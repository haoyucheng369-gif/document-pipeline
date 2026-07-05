using System.Text;
using Azure.Messaging.ServiceBus;
using CloudDocumentPipeline.Application.Abstractions.Messaging;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Azure Service Bus publisher used by cloud environments.
public sealed class ServiceBusJobMessagePublisher : IJobMessagePublisher
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSettings _settings;

    public ServiceBusJobMessagePublisher(
        ServiceBusClient serviceBusClient,
        ServiceBusSettings settings)
    {
        _serviceBusClient = serviceBusClient;
        _settings = settings;
    }

    public Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // The outbox already stores serialized integration messages, so raw publishing is required.
        throw new NotSupportedException("Use PublishRawAsync for integration messages.");
    }

    public async Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default)
    {
        // A short-lived sender is enough for the outbox publisher's low-volume batch loop.
        await using var sender = _serviceBusClient.CreateSender(_settings.TopicName);

        var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(payloadJson))
        {
            Subject = messageType,
            ContentType = "application/json"
        };

        // Store the type both as Subject and an application property for flexible consumers.
        message.ApplicationProperties["messageType"] = messageType;

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
