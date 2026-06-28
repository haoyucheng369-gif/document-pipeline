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

public sealed class NotificationWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    // Notification consumer 鍜?Job consumer 澶勭悊鐨勬槸鍚屼竴鏉′簨浠讹紝
    // 浣?Inbox 鍘婚噸蹇呴』鎸夋秷璐硅€呭尯鍒嗭紝鎵€浠ヨ繖閲岃鏈夌嫭绔嬬殑 ConsumerName銆?
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
        // Notification service 鍙闃呴€氱煡鐩稿叧闃熷垪锛屼笉璐熻矗 retry / DLQ 缂栨帓銆?
        _connection = _connectionProvider.GetConnection();
        _channel = _connection.CreateModel();
        _topologyInitializer.EnsureTopology(_channel);

        // 鎺у埗骞跺彂鎶撳彇閲忥紝閬垮厤涓€娆″湪鏈湴绉帇澶鏈‘璁ゆ秷鎭€?
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
            // 鍘熷娑堟伅鍏堣繕鍘熸垚 JSON锛屽啀鍙嶅簭鍒楀寲鎴愬绾﹀璞°€?
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            try
            {
                var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Notification message deserialization failed.");

                using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();
                    var sender = scope.ServiceProvider.GetRequiredService<NotificationEmailSender>();

                    // 閫氱煡娑堟伅鍚屾牱鍏?claim锛岄伩鍏嶉噸澶嶅彂閫侀€氱煡銆?
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

                    // 褰撳墠瀹炵幇鏄ā鎷熷彂閭欢锛屽悗缁彲浠ユ浛鎹㈡垚鐪熷疄閭欢鏈嶅姟鎴?webhook銆?
                    await sender.SendAsync(message, stoppingToken);

                    // 閫氱煡鎴愬姛鍚庤ˉ榻?Inbox 鏈€缁堢姸鎬併€?
                    inbox.MarkProcessed();
                    await inboxRepository.SaveChangesAsync(stoppingToken);

                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    _logger.LogInformation("Notification sent for job {JobId}.", message.JobId);
                }
            }
            catch (Exception ex)
            {
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
        // 杩欓噷鍙噴鏀惧綋鍓?channel锛岄暱杩炴帴鐢?ConnectionProvider 缁熶竴绠＄悊銆?
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
