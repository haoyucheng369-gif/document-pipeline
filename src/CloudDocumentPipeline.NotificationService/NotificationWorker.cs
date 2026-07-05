using System.Text;
using System.Text.Json;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Domain.Inbox;
using CloudDocumentPipeline.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace CloudDocumentPipeline.NotificationService;

// RabbitMQ notification consumer. It represents secondary side effects triggered by job events.
public sealed class NotificationWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    // Notification processing has a separate inbox identity from the job worker.
    private const string ConsumerName = "CloudDocumentPipeline.NotificationConsumer";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<NotificationWorker> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public NotificationWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings,
        ILogger<NotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = _connectionProvider.GetConnection();
        _channel = _connection.CreateModel();
        _topologyInitializer.EnsureTopology(_channel);

        // Keep notification throughput bounded so it cannot starve the database.
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _logger.LogInformation("Notification worker connected. Queue: {QueueName}", _settings.NotificationQueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            try
            {
                // Notification currently listens to job-created events and simulates delivery.
                var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Notification message deserialization failed.");

                using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();
                    var sender = scope.ServiceProvider.GetRequiredService<NotificationEmailSender>();

                    // Claim first so duplicate broker deliveries do not send duplicate notifications.
                    var claimed = await inboxRepository.TryClaimAsync(
                        message.MessageId,
                        ConsumerName,
                        TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds),
                        stoppingToken);

                    if (!claimed)
                    {
                        _logger.LogWarning("Notification message already claimed or processed. MessageId: {MessageId}", message.MessageId);
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        return;
                    }

                    var inbox = await inboxRepository.GetByMessageIdAsync(message.MessageId, ConsumerName, stoppingToken)
                        ?? throw new InvalidOperationException($"Inbox claim for notification message '{message.MessageId}' was not found.");

                    // Replace this sender with a real provider integration when email delivery is enabled.
                    await sender.SendAsync(message, stoppingToken);

                    // Complete the inbox after the side effect finishes.
                    inbox.MarkProcessed();
                    await inboxRepository.SaveChangesAsync(stoppingToken);

                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    _logger.LogInformation("Notification sent for job {JobId}.", message.JobId);
                }
            }
            catch (Exception ex)
            {
                // Notification failures are logged and acknowledged so they do not block core conversion.
                _logger.LogError(ex, "Error while processing notification message.");
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.NotificationQueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Notification consumer started.");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        // The shared connection provider owns the connection; this service owns only its channel.
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
