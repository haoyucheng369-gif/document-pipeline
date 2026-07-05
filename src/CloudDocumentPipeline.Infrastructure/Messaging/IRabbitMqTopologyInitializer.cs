using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Ensures RabbitMQ topology exists before publishers or consumers use it.
public interface IRabbitMqTopologyInitializer
{
    void EnsureTopology(IModel channel);
}
