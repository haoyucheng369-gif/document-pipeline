namespace CloudDocumentPipeline.Application.Abstractions.Processing;

public interface IJobSideEffectExecutor
{
    Task<string> ExecuteAsync(
        Guid jobId,
        string jobType,
        string payloadJson,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken = default);
}
