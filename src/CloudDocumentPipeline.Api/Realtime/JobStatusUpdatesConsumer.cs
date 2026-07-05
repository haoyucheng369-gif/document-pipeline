using System.Text;
using System.Text.Json;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CloudDocumentPipeline.Api.Realtime;

// RabbitMQ-backed realtime bridge from job.status.changed events to SignalR clients.
public sealed class JobStatusUpdatesConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;
    private readonly IHubContext<JobUpdatesHub> _hubContext;
    private readonly ILogger<JobStatusUpdatesConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public JobStatusUpdatesConsumer(
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings,
        IHubContext<JobUpdatesHub> hubContext,
        ILogger<JobStatusUpdatesConsumer> logger)
    {
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
        _hubContext = hubContext;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // The API owns a separate queue for realtime updates so worker and notification consumers stay isolated.
        _connection = _connectionProvider.GetConnection();
        _channel = _connection.CreateModel();
        _topologyInitializer.EnsureTopology(_channel);

        _logger.LogInformation(
            "Job status updates consumer connected. Queue: {QueueName}",
            _settings.JobStatusUpdatesQueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            try
            {
                // Realtime consumers care only about the small status payload needed by the browser.
                var message = JsonSerializer.Deserialize<JobStatusChangedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Status change message deserialization failed.");

                await _hubContext.Clients.All.SendAsync(
                    "jobUpdated",
                    new
                    {
                        jobId = message.JobId,
                        status = message.Status,
                        retryCount = message.RetryCount
                    },
                    stoppingToken);

                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
            catch (Exception exception)
            {
                // Bad realtime messages are acknowledged after logging; the source of truth remains the database.
                _logger.LogError(exception, "Failed to process job status update message: {Message}", json);
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.JobStatusUpdatesQueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Job status updates consumer started.");
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
