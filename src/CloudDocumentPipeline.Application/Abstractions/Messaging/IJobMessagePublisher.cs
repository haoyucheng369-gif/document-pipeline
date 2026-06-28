namespace CloudDocumentPipeline.Application.Abstractions.Messaging;

public interface IJobMessagePublisher
{
    Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default);
}
