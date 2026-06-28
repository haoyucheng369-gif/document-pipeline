using System.Text.Json;
using Microsoft.Extensions.Logging;
using CloudDocumentPipeline.Application.Messaging;

namespace CloudDocumentPipeline.NotificationService;

public sealed class NotificationEmailSender
{
    private readonly ILogger<NotificationEmailSender> _logger;

    public NotificationEmailSender(ILogger<NotificationEmailSender> logger)
    {
        _logger = logger;
    }

    public Task<string> SendAsync(JobCreatedIntegrationMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Simulated email sent. JobId: {JobId}, Recipient: {RecipientEmail}, Subject: {Subject}, IdempotencyKey: {IdempotencyKey}",
            message.JobId,
            "ops@docflowcloud.local",
            $"Job {message.JobId} was accepted",
            message.IdempotencyKey);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            message.JobId,
            recipientEmail = "ops@docflowcloud.local",
            subject = $"Job {message.JobId} was accepted",
            message.IdempotencyKey,
            sentAtUtc = DateTime.UtcNow,
            provider = "simulation"
        }));
    }
}
