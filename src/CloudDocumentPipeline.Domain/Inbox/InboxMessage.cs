using Stateless;

namespace CloudDocumentPipeline.Domain.Inbox;

// Inbox 璁板綍鐨勬槸鈥滄煇涓?consumer 濡備綍澶勭悊鏌愭潯娑堟伅鈥濓紝
// 鏍稿績鐩爣鏄幓閲嶃€佹姠鍗犲鐞嗘潈銆佽窡韪鐞嗕腑/宸插畬鎴?澶辫触鐘舵€併€?
public sealed class InboxMessage
{
    private readonly StateMachine<InboxStatus, Trigger> _stateMachine;

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public string ConsumerName { get; private set; } = default!;
    public InboxStatus Status { get; private set; }
    public DateTime ClaimedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? ErrorMessage { get; private set; }

    private InboxMessage()
    {
        // 渚?EF Core 鍙嶅皠鏋勯€犱娇鐢ㄣ€?
        _stateMachine = CreateStateMachine();
    }

    public InboxMessage(Guid messageId, string consumerName)
    {
        // 鎴愬姛 claim 涓€鏉℃秷鎭椂锛屼細鏂板缓涓€鏉?Inbox 璁板綍骞剁洿鎺ヨ繘鍏?Processing銆?
        Id = Guid.NewGuid();
        MessageId = messageId;
        ConsumerName = consumerName;
        Status = InboxStatus.Processing;
        ClaimedAtUtc = DateTime.UtcNow;
        _stateMachine = CreateStateMachine();
    }

    public bool IsStale(DateTime utcNow, TimeSpan processingTimeout)
    {
        // 濡傛灉鏌愭潯娑堟伅闀挎湡鍋滃湪 Processing锛屽彲浠ヨ涓烘棫澶勭悊宸茬粡鍗℃锛屽厑璁稿悗缁帴绠°€?
        return Status == InboxStatus.Processing && ClaimedAtUtc.Add(processingTimeout) <= utcNow;
    }

    public void Reclaim()
    {
        // 閲嶆柊 claim 鏈川涓婃槸鈥滈噸鏂板紑濮嬪綋鍓嶈繖鏉℃秷鎭殑澶勭悊鏉冪敓鍛藉懆鏈熲€濄€?
        _stateMachine.Fire(Trigger.Reclaim);
    }

    public void MarkProcessed()
    {
        // 褰撳墠 consumer 宸插畬鎴愯繖鏉℃秷鎭€?
        _stateMachine.Fire(Trigger.Complete);
    }

    public void MarkFailed(string errorMessage)
    {
        // 褰撳墠 consumer 杩欐澶勭悊澶辫触锛岃褰曢敊璇苟杞叆 Failed銆?
        ErrorMessage = errorMessage;
        _stateMachine.Fire(Trigger.Fail);
    }

    private StateMachine<InboxStatus, Trigger> CreateStateMachine()
    {
        // Inbox 鐘舵€佹満鏄秷鎭秷璐瑰眰鐨勪繚鎶わ紝涓嶆槸涓氬姟灞傜姸鎬佹満銆?
        var stateMachine = new StateMachine<InboxStatus, Trigger>(
            () => Status,
            status => Status = status);

        stateMachine.Configure(InboxStatus.Processing)
            // 姝ｅ湪澶勭悊鏃讹紝鍙厑璁哥粨鏉熸垚鍔熴€佺粨鏉熷け璐ワ紝鎴栬€呰閲嶆柊鎺ョ銆?
            .Permit(Trigger.Complete, InboxStatus.Processed)
            .Permit(Trigger.Fail, InboxStatus.Failed)
            .PermitReentry(Trigger.Reclaim)
            .OnEntry(() =>
            {
                // 姣忔杩涘叆 Processing锛堥娆?claim 鎴?reclaim锛夐兘鍒锋柊 claim 鏃堕棿銆?
                ClaimedAtUtc = DateTime.UtcNow;
                ProcessedAtUtc = null;
                ErrorMessage = null;
            });

        stateMachine.Configure(InboxStatus.Processed)
            .OnEntry(() =>
            {
                // 鎴愬姛澶勭悊鍚庤褰曞畬鎴愭椂闂淬€?
                ProcessedAtUtc = DateTime.UtcNow;
                ErrorMessage = null;
            });

        stateMachine.Configure(InboxStatus.Failed)
            .OnEntry(() =>
            {
                // 澶辫触涔熺畻涓€绉嶁€滃凡缁撴潫鈥濓紝鎵€浠ュ悓鏍疯褰曠粨鏉熸椂闂淬€?
                ProcessedAtUtc = DateTime.UtcNow;
            })
            .Permit(Trigger.Reclaim, InboxStatus.Processing);

        stateMachine.OnUnhandledTrigger((state, trigger) =>
        {
            // 闈炴硶娑堟伅鐘舵€佹祦杞洿鎺ユ姏寮傚父锛岄伩鍏嶈闈欓粯蹇界暐銆?
            throw new InvalidOperationException(CreateUnhandledTriggerMessage(state, trigger));
        });

        return stateMachine;
    }

    private static string CreateUnhandledTriggerMessage(InboxStatus state, Trigger trigger)
    {
        // 鎻愪緵鏇村ソ鐞嗚В鐨勯敊璇枃鏈紝鏂逛究鎭㈠閫昏緫涓庢祴璇曟帓鏌ャ€?
        return (state, trigger) switch
        {
            (InboxStatus.Processed, Trigger.Complete) => "Only processing inbox messages can be marked as processed.",
            (InboxStatus.Processed, Trigger.Fail) => "Only processing inbox messages can be marked as failed.",
            (InboxStatus.Processed, Trigger.Reclaim) => "Only failed or processing inbox messages can be reclaimed.",
            (InboxStatus.Failed, Trigger.Complete) => "Only processing inbox messages can be marked as processed.",
            _ => $"Trigger '{trigger}' is not valid for state '{state}'."
        };
    }

    private enum Trigger
    {
        // Trigger 浠ｈ〃鈥滄秷鎭秷璐规祦绋嬮噷鍙戠敓浜嗕粈涔堝姩浣溾€濄€?
        Complete,
        Fail,
        Reclaim
    }
}
