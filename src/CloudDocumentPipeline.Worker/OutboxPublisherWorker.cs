using CloudDocumentPipeline.Application.Abstractions.Messaging;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudDocumentPipeline.Worker;

// Polls persisted outbox rows and publishes them through the configured message broker.
public sealed class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox publisher worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Use a fresh scope per poll so each batch gets a short-lived DbContext.
                using var scope = _scopeFactory.CreateScope();

                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                var publisher = scope.ServiceProvider.GetRequiredService<IJobMessagePublisher>();

                var messages = await outboxRepository.GetUnprocessedAsync(20, stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        // Mark as processed only after the broker publish succeeds.
                        await publisher.PublishRawAsync(message.Type, message.PayloadJson, stoppingToken);
                        message.MarkProcessed();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish outbox message {OutboxMessageId}", message.Id);
                        message.MarkFailed(ex.Message);
                    }
                }

                await outboxRepository.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in outbox publisher worker.");
            }

            // Outbox polling is intentionally simple; broker failures leave rows for the next scan.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
