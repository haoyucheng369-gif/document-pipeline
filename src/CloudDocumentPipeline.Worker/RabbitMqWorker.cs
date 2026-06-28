using System.Text;
using System.Text.Json;
using CloudDocumentPipeline.Application.Abstractions.Messaging;
using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Application.Abstractions.Processing;
using CloudDocumentPipeline.Application.Exceptions;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Domain.Inbox;
using CloudDocumentPipeline.Domain.Jobs;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace CloudDocumentPipeline.Worker;

public sealed class RabbitMqWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    // 鍚屼竴鏉℃秷鎭細琚笉鍚?consumer 鍒嗗埆澶勭悊锛屾墍浠ヨ繖閲岀殑娑堣垂鑰呭悕绉拌鍥哄畾銆?
    // Inbox 鍘婚噸涓?claim 鎶㈠崰渚濊禆 (MessageId, ConsumerName) 杩欑粍鍞竴閿€?
    private const string ConsumerName = "CloudDocumentPipeline.JobConsumer";
    // 瓒呰繃鏈€澶ч噸璇曟鏁板悗锛岃繖鏉℃秷鎭笉鍐嶅洖涓绘祦绋嬶紝鑰屾槸杩涘叆 DLQ 绛夊緟鎺掓煡銆?
    private const int MaxRetryCount = 3;
    // 绠€鍗曢樁姊紡 backoff锛氱 1/2/3 娆￠噸璇曞垎鍒欢杩?1s / 5s / 30s銆?
    private static readonly int[] RetryDelaysInSeconds = [1, 5, 30];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqWorker> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings,
        ILogger<RabbitMqWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // StartAsync 鍙礋璐ｅ噯澶?RabbitMQ 娑堣垂閫氶亾銆?
        // 闀胯繛鎺ヤ氦缁欏叕鍏?ConnectionProvider 澶嶇敤锛屾嫇鎵戝０鏄庝氦缁欏叕鍏卞垵濮嬪寲鍣ㄣ€?
        _connection = _connectionProvider.GetConnection();
        _channel = _connection.CreateModel();
        _topologyInitializer.EnsureTopology(_channel);

        // 闄愬埗鍗曚釜 consumer 鍚屾椂鎶撳彇鍦ㄦ墜涓婄殑鏈‘璁ゆ秷鎭暟锛岄伩鍏嶇灛闂存媺澶娑堟伅銆?
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _logger.LogInformation("RabbitMQ worker connected. Queue: {QueueName}", _settings.QueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        // EventingBasicConsumer 鏄€滄秷鎭埌浜嗗啀鍥炶皟鎴戔€濈殑浜嬩欢椹卞姩妯″紡銆?
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            // 鍏堟嬁鍒?RabbitMQ 閲岀殑鍘熷娑堟伅浣擄紝鍚庨潰鎵€鏈夊鐞嗛兘鍩轰簬杩欎唤 JSON銆?
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            _logger.LogInformation("Received message: {Message}", json);

            try
            {
                // 鍏堟妸娑堟伅浣撹繕鍘熸垚搴旂敤灞傚绾﹀璞★紝鍚庨潰涓氬姟閫昏緫閮藉洿缁曡繖涓璞″睍寮€銆?
                var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions)
                    ?? throw new InvalidOperationException("Message deserialization failed.");

                // 鎶?CorrelationId 鍘嬭繘鏃ュ織涓婁笅鏂囷紝鍚庣画杩欐潯娑堟伅澶勭悊閾句笂鐨勬棩蹇楀氨鑳戒覆璧锋潵銆?
                using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();

                    // 鍏?claim 鍐嶅鐞嗭細鍙湁鎶㈠埌澶勭悊鏉冪殑瀹炰緥鎵嶈兘缁х画鎵ц涓氬姟閫昏緫銆?
                    var claimed = await inboxRepository.TryClaimAsync(
                        message.MessageId,
                        ConsumerName,
                        TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds),
                        stoppingToken);

                    if (!claimed)
                    {
                        _logger.LogWarning("Message was already claimed or processed. MessageId: {MessageId}", message.MessageId);
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        return;
                    }

                    // 濡傛灉涓氬姟宸茬粡鎴愬姛杩囦簡锛屽彧琛ラ綈 Inbox 鐘舵€侊紝涓嶅啀閲嶅鎵ц鍓綔鐢ㄣ€?
                    var completion = await TryCompleteAlreadySucceededJobAsync(message, stoppingToken);
                    if (completion)
                    {
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        _logger.LogInformation("Job {JobId} was already succeeded. Side effects were skipped.", message.JobId);
                        return;
                    }

                    // 鐪熸鐨勪笟鍔″壇浣滅敤鏀惧湪鐙珛鏈嶅姟閲屾墽琛岋紝Worker 杩欓噷鍙礋璐ｇ紪鎺掍笌鐘舵€佹帶鍒躲€?
                    var resultJson = await ExecuteSideEffectsAsync(message, stoppingToken);

                    // 鎴愬姛璺緞閲屾妸 Job 鍜?Inbox 涓€璧锋彁浜わ紝閬垮厤鐘舵€佷笉涓€鑷淬€?
                    await CompleteMessageAsync(message, resultJson, stoppingToken);

                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    _logger.LogInformation("Job {JobId} processed successfully.", message.JobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing message.");

                await HandleFailureAsync(json, ex, body, eventArgs.BasicProperties, stoppingToken);
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("RabbitMQ consumer started.");

        return Task.CompletedTask;
    }

    private async Task<bool> TryCompleteAlreadySucceededJobAsync(
        JobCreatedIntegrationMessage message,
        CancellationToken cancellationToken)
    {
        // 涓氬姟骞傜瓑淇濇姢锛?
        // 濡傛灉 Job 宸茬粡鎴愬姛锛屽垯鍙渶瑕佹妸褰撳墠 Inbox 浠?Processing 琛ユ垚 Processed銆?
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        if (job.Status != JobStatus.Succeeded)
        {
            return false;
        }

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
            x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
            cancellationToken)
            ?? throw new InvalidOperationException($"Inbox claim for message '{message.MessageId}' was not found.");

        if (inbox.Status == InboxStatus.Processing)
        {
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return true;
    }

    private async Task<string> ExecuteSideEffectsAsync(
        JobCreatedIntegrationMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jobMetrics = scope.ServiceProvider.GetRequiredService<IJobMetrics>();
        var executor = scope.ServiceProvider.GetRequiredService<IJobSideEffectExecutor>();

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        return await executor.ExecuteAsync(
            job.Id,
            job.Type,
            job.PayloadJson,
            message.IdempotencyKey,
            message.CorrelationId,
            cancellationToken);
    }

    private async Task CompleteMessageAsync(
        JobCreatedIntegrationMessage message,
        string resultJson,
        CancellationToken cancellationToken)
    {
        // 鎴愬姛璺緞鐨勬暟鎹簱浜嬪姟杈圭晫锛?
        // Job 鐘舵€佹洿鏂?+ Inbox 鐘舵€佹洿鏂板繀椤讳竴璧锋彁浜ゃ€?
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jobMetrics = scope.ServiceProvider.GetRequiredService<IJobMetrics>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken)
            ?? throw new JobNotFoundException(message.JobId);

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
            x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
            cancellationToken)
            ?? throw new InvalidOperationException($"Inbox claim for message '{message.MessageId}' was not found.");

        if (job.Status == JobStatus.Succeeded)
        {
            // 鍙屼繚闄╋細鍗充娇鍒拌繖閲屾墠鍙戠幇 Job 宸叉垚鍔燂紝涔熶笉瑕侀噸澶嶆墽琛屼笟鍔″畬鎴愰€昏緫銆?
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        // 鍙湁 Pending -> Processing -> Succeeded 杩欐潯鍚堟硶璺緞鎵嶈兘璧板埌杩欓噷銆?
        job.MarkProcessing();
        job.MarkSucceeded(resultJson);
        inbox.MarkProcessed();

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        jobMetrics.JobSucceeded(job.Type);
        RecordProcessingDuration(job, jobMetrics);

        await PublishJobStatusChangedAsync(
            message.JobId,
            JobStatus.Succeeded,
            job.RetryCount,
            message.CorrelationId,
            cancellationToken);
    }

    private async Task HandleFailureAsync(
        string json,
        Exception exception,
        byte[] body,
        IBasicProperties? properties,
        CancellationToken cancellationToken)
    {
        // 澶辫触澶勭悊鍒嗕袱灞傦細
        // 1. 鍏堟妸鏁版嵁搴撻噷鐨?Job / Inbox 鐘舵€佷慨姝ｄ负澶辫触
        // 2. 鍐嶅喅瀹氭秷鎭眰鏄噸璇曡繕鏄繘鍏?DLQ
        await TryMarkFailedAsync(json, exception, cancellationToken);

        try
        {
            var disposition = MessageFailureClassification.Classify(exception);
            var retryCount = GetRetryCount(properties);

            if (disposition == MessageFailureDisposition.Retry && retryCount < MaxRetryCount)
            {
                // 鍙噸璇曢敊璇蛋 backoff锛氭秷鎭厛杩涘叆 retry queue锛岃繃 TTL 鍐嶅洖涓婚槦鍒椼€?
                RepublishWithRetry(body, retryCount + 1);
                _logger.LogWarning(
                    "Retryable error. Message scheduled for retry {RetryCount} after {DelaySeconds}s",
                    retryCount + 1,
                    GetRetryDelaySeconds(retryCount + 1));
            }
            else
            {
                // 涓嶅彲閲嶈瘯锛屾垨閲嶈瘯娆℃暟宸茬敤灏斤紝杞叆 DLQ銆?
                PublishToDeadLetter(body);
                _logger.LogError(
                    "Message moved to DLQ. Disposition: {Disposition}, RetryCount: {RetryCount}",
                    disposition,
                    retryCount);
            }
        }
        catch (Exception retryEx)
        {
            _logger.LogError(retryEx, "Failed during retry/DLQ handling.");
        }
    }

    private async Task TryMarkFailedAsync(string json, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            // 灏介噺鎶婂け璐ョ姸鎬佽惤鍥炴暟鎹簱锛岄伩鍏?Job / Inbox 涓€鐩村崱鍦?Processing銆?
            var message = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(json, JsonSerializerOptions);
            if (message is null)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jobMetrics = scope.ServiceProvider.GetRequiredService<IJobMetrics>();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken);
            var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(
                x => x.MessageId == message.MessageId && x.ConsumerName == ConsumerName,
                cancellationToken);

            if (job is not null && job.Status != JobStatus.Succeeded && job.Status != JobStatus.Failed)
            {
                job.MarkFailed(exception.Message);
            }

            if (inbox is not null && inbox.Status == InboxStatus.Processing)
            {
                inbox.MarkFailed(exception.Message);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (job is not null && job.Status == JobStatus.Failed)
            {
                jobMetrics.JobFailed(job.Type);
                RecordProcessingDuration(job, jobMetrics);
                await PublishJobStatusChangedAsync(
                    job.Id,
                    JobStatus.Failed,
                    job.RetryCount,
                    message.CorrelationId,
                    cancellationToken);
            }
        }
        catch (Exception markFailedException)
        {
            _logger.LogError(markFailedException, "Failed to mark job and inbox as failed after processing error.");
        }
    }

    private async Task PublishJobStatusChangedAsync(
        Guid jobId,
        JobStatus status,
        int retryCount,
        string correlationId,
        CancellationToken cancellationToken)
    {
        // 鐘舵€佸彉鍖栭€氱煡涓嶆槸涓讳笟鍔℃秷鎭紝鎵€浠ヨ繖閲岀洿鎺ュ鐢ㄧ粺涓€鍙戝竷鍣ㄥ彂閫併€?
        using var scope = _scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IJobMessagePublisher>();

        var message = new JobStatusChangedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = jobId,
            Status = status.ToString(),
            RetryCount = retryCount,
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        };

        var payloadJson = JsonSerializer.Serialize(message, JsonSerializerOptions);

        await publisher.PublishRawAsync(
            nameof(JobStatusChangedIntegrationMessage),
            payloadJson,
            cancellationToken);
    }

    private int GetRetryCount(IBasicProperties? properties)
    {
        // 閲嶈瘯娆℃暟瀛樻斁鍦?RabbitMQ header 閲岋紝鑰屼笉鏄秷鎭綋閲屻€?
        if (properties?.Headers is null)
            return 0;

        if (!properties.Headers.TryGetValue("x-retry-count", out var value))
            return 0;

        if (value is byte[] bytes && int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed))
            return parsed;

        if (value is int intValue)
            return intValue;

        return 0;
    }

    private void RepublishWithRetry(byte[] body, int retryCount)
    {
        if (_channel is null)
            return;

        // 寤惰繜閲嶈瘯锛?
        // 鍏堝彂鍒?retry queue锛屽苟璁剧疆 TTL锛?
        // TTL 鍒版湡鍚庯紝RabbitMQ 浼氶€氳繃 dead-letter 鎶婃秷鎭噸鏂拌矾鐢卞洖涓婚槦鍒椼€?
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Expiration = TimeSpan.FromSeconds(GetRetryDelaySeconds(retryCount)).TotalMilliseconds.ToString("F0");
        properties.Headers = new Dictionary<string, object>
        {
            ["x-retry-count"] = retryCount.ToString()
        };

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.RetryQueueName,
            basicProperties: properties,
            body: body);
    }

    private static int GetRetryDelaySeconds(int retryCount)
    {
        var index = Math.Clamp(retryCount - 1, 0, RetryDelaysInSeconds.Length - 1);
        return RetryDelaysInSeconds[index];
    }

    private void PublishToDeadLetter(byte[] body)
    {
        if (_channel is null)
            return;

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.DeadLetterQueueName,
            basicProperties: properties,
            body: body);
    }

    private static void RecordProcessingDuration(Job job, IJobMetrics jobMetrics)
    {
        if (job.StartedAtUtc is null || job.CompletedAtUtc is null)
        {
            return;
        }

        var durationSeconds = (job.CompletedAtUtc.Value - job.StartedAtUtc.Value).TotalSeconds;
        if (durationSeconds >= 0)
        {
            jobMetrics.RecordProcessingDuration(job.Type, durationSeconds);
        }
    }

    public override void Dispose()
    {
        // Hosted service 閫€鍑烘椂鍙叧闂綋鍓?channel銆?
        // 闀胯繛鎺ョ敱 ConnectionProvider 鎸夎繘绋嬬粺涓€澶嶇敤鍜岄噴鏀俱€?
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
