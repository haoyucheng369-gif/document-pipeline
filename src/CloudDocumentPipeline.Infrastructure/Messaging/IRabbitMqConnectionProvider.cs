using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Provides a shared RabbitMQ connection; callers create short-lived channels from it.
public interface IRabbitMqConnectionProvider : IDisposable
{
    IConnection GetConnection();
}
