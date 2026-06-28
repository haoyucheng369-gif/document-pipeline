using System.Text.Json;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Domain.Inbox;
using CloudDocumentPipeline.Domain.Jobs;
using CloudDocumentPipeline.Domain.Outbox;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudDocumentPipeline.Worker;

public sealed class StaleInboxRecoveryWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    // 褰撳墠鎭㈠鍣ㄥ彧澶勭悊 Job consumer 鐨勫崱姝绘秷鎭€?
    // Notification consumer 鐨勫鐞嗛€昏緫杈冭交锛屽厛涓嶅仛鑷姩鎭㈠缂栨帓銆?
    private const string ConsumerName = "CloudDocumentPipeline.JobConsumer";
    private const int MaxRecoveryRetryCount = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<StaleInboxRecoveryWorker> _logger;

    public StaleInboxRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        RabbitMqSettings settings,
        ILogger<StaleInboxRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 杩欐槸涓€涓畾鏃舵仮澶嶅櫒锛?
        // 姣忛殧涓€娈垫椂闂存壂鎻忎竴娆?Inbox锛屽鎵鹃暱鏃堕棿鍋滃湪 Processing 鐨勬秷鎭€?
        _logger.LogInformation("Stale inbox recovery worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverStaleInboxesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while recovering stale inbox messages.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.StaleRecoveryScanSeconds), stoppingToken);
        }
    }

    private async Task RecoverStaleInboxesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 瓒呰繃 ProcessingTimeoutSeconds 杩樻病瀹屾垚鐨?Inbox锛岃涓哄凡缁?stale銆?
        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(-_settings.ProcessingTimeoutSeconds);

        // 杩欓噷鍙壂鎻?Job consumer 涓嬭秴鏃剁殑 Processing 璁板綍銆?
        // 涓€娆℃渶澶氬鐞?20 鏉★紝閬垮厤鎭㈠浠诲姟鏈韩鎶婃暟鎹簱鎵撳緱杩囬噸銆?
        var staleInboxIds = await dbContext.InboxMessages
            .Where(x =>
                x.ConsumerName == ConsumerName &&
                x.Status == InboxStatus.Processing &&
                x.ClaimedAtUtc <= staleBeforeUtc)
            .OrderBy(x => x.ClaimedAtUtc)
            .Select(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var inboxId in staleInboxIds)
        {
            await RecoverSingleInboxAsync(inboxId, cancellationToken);
        }
    }

    private async Task RecoverSingleInboxAsync(Guid inboxId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 鍗曟潯鎭㈠蹇呴』鍦ㄤ簨鍔￠噷瀹屾垚锛屼繚璇?Job銆両nbox銆丱utbox 涓夎€呯姸鎬佸悓姝ャ€?
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(x => x.Id == inboxId, cancellationToken);
        if (inbox is null ||
            inbox.ConsumerName != ConsumerName ||
            !inbox.IsStale(DateTime.UtcNow, TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds)))
        {
            // 閲嶆柊璇诲彇鍚庡鏋滃彂鐜板凡缁忎笉 stale锛屽氨璇存槑鍒殑瀹炰緥鍙兘宸茬粡澶勭悊浜嗭紝鐩存帴璺宠繃銆?
            return;
        }

        // 閫氳繃鍘熷 Outbox 娑堟伅鎵惧埌褰撴椂瑙﹀彂杩欐澶勭悊鐨勬秷鎭綋锛?
        // 鍚庨潰闇€瑕佸熀浜庡畠鏋勯€犱竴鏉℃柊鐨?replay 娑堟伅銆?
        var originalOutbox = await dbContext.OutboxMessages
            .Where(x =>
                x.Type == nameof(JobCreatedIntegrationMessage) &&
                x.PayloadJson.Contains(inbox.MessageId.ToString()))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (originalOutbox is null)
        {
            // 鎵句笉鍒板師濮嬫秷鎭椂锛屽彧鑳芥妸褰撳墠 Inbox 鏀跺彛涓哄け璐ワ紝閬垮厤姘歌繙鍗″湪 Processing銆?
            inbox.MarkFailed("Processing timed out, but the original integration message could not be found.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because its original message could not be resolved.",
                inbox.Id);
            return;
        }

        var originalMessage = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(originalOutbox.PayloadJson, JsonSerializerOptions);
        if (originalMessage is null || originalMessage.MessageId != inbox.MessageId)
        {
            // 鍘熷娑堟伅鏍煎紡鎹熷潖鏃讹紝涓嶈兘鐩茬洰閲嶆斁锛屽厛鏄庣‘钀芥垚澶辫触锛岀瓑寰呬汉宸ュ鐞嗐€?
            inbox.MarkFailed("Processing timed out, but the original integration message payload was invalid.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because its original message payload was invalid.",
                inbox.Id);
            return;
        }

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == originalMessage.JobId, cancellationToken);
        if (job is null)
        {
            // Job 宸蹭笉瀛樺湪鏃讹紝涔熶笉鑳界户缁仮澶嶏紝鍙兘鎶婃秷鎭晶澶辫触鏀跺彛銆?
            inbox.MarkFailed("Processing timed out, but the related job could not be found.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because job {JobId} no longer exists.",
                inbox.Id,
                originalMessage.JobId);
            return;
        }

        if (job.Status == JobStatus.Succeeded)
        {
            // 濡傛灉涓氬姟鍏跺疄宸茬粡鎴愬姛锛屽彧鏄棫 Inbox 娌℃潵寰楀強鏀跺熬锛?
            // 閭ｅ氨鐩存帴鎶?Inbox 琛ユ垚 Processed锛屼笉鍐嶅彂 replay銆?
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Stale inbox {InboxId} was completed because job {JobId} had already succeeded.",
                inbox.Id,
                job.Id);
            return;
        }

        // 鍗℃鎭㈠涔熼渶瑕佷笂闄愭帶鍒讹紝鍚﹀垯鍚屼竴鏉℃秷鎭鏋滀竴鐩村崱姝伙紝灏变細涓嶆柇 replay銆?
        // 杩欓噷澶嶇敤 Job.RetryCount 浣滀负鎬婚噸璇曡鏁帮紝璁┾€滄櫘閫氬け璐ラ噸璇曗€濆拰鈥滃崱姝绘仮澶嶉噸璇曗€?
        // 閮借兘鍦ㄥ悓涓€涓笂闄愰噷琚害鏉熶綇銆?
        var projectedRetryCount = job.Status switch
        {
            JobStatus.Pending or JobStatus.Processing => job.RetryCount + 1,
            JobStatus.Failed => job.RetryCount,
            _ => job.RetryCount
        };

        if (projectedRetryCount >= MaxRecoveryRetryCount)
        {
            if (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
            {
                job.MarkFailed("Processing timed out. Recovery retry limit reached.");
            }

            inbox.MarkFailed("Processing timed out. Recovery retry limit reached.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale recovery retry limit reached for job {JobId}. InboxId: {InboxId}, RetryCount: {RetryCount}, Limit: {Limit}",
                job.Id,
                inbox.Id,
                projectedRetryCount,
                MaxRecoveryRetryCount);
            return;
        }

        if (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
        {
            // Job 杩樻病鐪熸瀹屾垚鏃讹紝鍏堟妸鏃у鐞嗘槑纭爣鎴愬け璐ワ紝鍐嶈蛋 Retry 鍥炲埌 Pending銆?
            // 杩欐牱涓嶄細缁曡繃鐘舵€佹満锛屼篃涓嶄細鐩存帴鎵嬫敼鎴愭煇涓粓鎬併€?
            job.MarkFailed("Processing timed out. Recovery replay scheduled.");
            job.Retry();
        }
        else if (job.Status == JobStatus.Failed)
        {
            // 濡傛灉涓氬姟宸茬粡鏄?Failed锛屽氨鐩存帴璧?Retry 鍥炲埌 Pending銆?
            job.Retry();
        }

        // 鏃?Inbox 杩欐鎭㈠娴佺▼灏辨敹鍙ｄ负澶辫触锛岄伩鍏嶇户缁崰鐫€ Processing銆?
        inbox.MarkFailed("Processing timed out. Recovery replay scheduled.");

        // 閲嶆柊鐢熸垚涓€鏉℃柊鐨勪笟鍔℃秷鎭紝璁╃郴缁熸寜姝ｅ父涓绘祦绋嬪啀璺戜竴娆°€?
        // 杩欓噷淇濈暀鍘熸潵鐨?CorrelationId 鍜?IdempotencyKey锛屾柟渚夸覆鑱旀棩蹇楀拰淇濊瘉涓氬姟骞傜瓑銆?
        var replayMessage = new JobCreatedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = job.Id,
            CorrelationId = originalMessage.CorrelationId,
            IdempotencyKey = originalMessage.IdempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        var replayPayload = JsonSerializer.Serialize(replayMessage, JsonSerializerOptions);
        dbContext.OutboxMessages.Add(new OutboxMessage(nameof(JobCreatedIntegrationMessage), replayPayload));

        // 浜嬪姟鎻愪氦鍚庯紝OutboxPublisherWorker 浼氭寜鍘熸潵鐨勬満鍒舵妸 replay 娑堟伅鍙戝嚭鍘汇€?
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogWarning(
            "Recovered stale inbox {InboxId} for job {JobId}. Recovery replay scheduled. RecoveryRetryCount: {RetryCount}",
            inbox.Id,
            job.Id,
            projectedRetryCount);
    }
}
