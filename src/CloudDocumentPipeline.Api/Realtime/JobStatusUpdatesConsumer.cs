using System.Text;
using System.Text.Json;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CloudDocumentPipeline.Api.Realtime;

// Job 鐘舵€佸彉鍖栨秷璐硅€咃細
// API 杩涚▼璁㈤槄 Worker 鍙戝嚭鐨?job.status.changed 浜嬩欢锛?
// 鍐嶉€氳繃 SignalR 鎶婄姸鎬佸彉鍖栧箍鎾粰鍓嶇椤甸潰銆?
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
        // API 渚ф秷璐硅€呭彧闇€瑕佹嬁鍒颁竴涓秷璐?channel锛屽苟纭繚鐘舵€佹洿鏂伴槦鍒楁嫇鎵戝瓨鍦ㄣ€?
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
                // 鍏堟妸 MQ 閲岀殑鐘舵€佸彉鍖栨秷鎭繕鍘熸垚搴旂敤灞傚绾︺€?
                var message = JsonSerializer.Deserialize<JobStatusChangedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Status change message deserialization failed.");

                // API 渚у彧鍋氫竴浠朵簨锛氭妸鐘舵€佸彉鍖栬浆鎴愬墠绔兘鐩戝惉鐨?SignalR 浜嬩欢銆?
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
        // 杩欓噷鍙噴鏀惧綋鍓?channel锛岄暱杩炴帴鐢?ConnectionProvider 缁熶竴绠＄悊銆?
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
