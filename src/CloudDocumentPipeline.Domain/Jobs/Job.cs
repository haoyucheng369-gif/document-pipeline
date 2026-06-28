using Stateless;

namespace CloudDocumentPipeline.Domain.Jobs;

// 棰嗗煙鑱氬悎锛氳〃绀轰竴涓紓姝ヤ笟鍔′换鍔°€?
// 杩欎釜绫荤殑鏍稿績鑱岃矗涓嶆槸鈥滃瓨鏁版嵁鈥濓紝鑰屾槸绾︽潫浠诲姟鐘舵€佸彧鑳芥寜鍚堟硶椤哄簭娴佽浆銆?
public class Job
{
    private readonly StateMachine<JobStatus, Trigger> _stateMachine;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Type { get; private set; } = default!;
    public JobStatus Status { get; private set; }
    public string PayloadJson { get; private set; } = default!;
    public string? ResultJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private Job()
    {
        // 渚?EF Core 鍙嶅皠鏋勯€犱娇鐢ㄣ€?
        _stateMachine = CreateStateMachine();
    }

    public Job(string name, string type, string payloadJson)
    {
        // 鏂颁换鍔″垱寤烘椂榛樿浠?Pending 寮€濮嬨€?
        Id = Guid.NewGuid();
        Name = name;
        Type = type;
        PayloadJson = payloadJson;
        Status = JobStatus.Pending;
        RetryCount = 0;
        CreatedAtUtc = DateTime.UtcNow;
        _stateMachine = CreateStateMachine();
    }

    public void MarkProcessing()
    {
        // 杩涘叆澶勭悊涓€?
        _stateMachine.Fire(Trigger.StartProcessing);
    }

    public void MarkSucceeded(string resultJson)
    {
        // 鍏堜繚瀛樼粨鏋滐紝鍐嶆帹杩涚姸鎬併€?
        ResultJson = resultJson;
        _stateMachine.Fire(Trigger.Succeed);
    }

    public void MarkFailed(string errorMessage)
    {
        // 鍏堣褰曢敊璇紝鍐嶆帹杩涚姸鎬併€?
        ErrorMessage = errorMessage;
        _stateMachine.Fire(Trigger.Fail);
    }

    public void Retry()
    {
        // 浠呭厑璁?Failed -> Pending銆?
        _stateMachine.Fire(Trigger.Retry);
    }

    private StateMachine<JobStatus, Trigger> CreateStateMachine()
    {
        // 鐘舵€佹満缁熶竴瀹氫箟鍝簺鐘舵€佸彲浠ュ浣曡浆鎹紝浠ュ強杩涘叆鐘舵€佹椂瑕佸仛浠€涔堝壇浣滅敤銆?
        var stateMachine = new StateMachine<JobStatus, Trigger>(
            () => Status,
            status => Status = status);

        stateMachine.Configure(JobStatus.Pending)
            // 鏂板缓鎴栭噸璇曞悗鐨勪换鍔★紝涓嬩竴姝ュ彧鑳借繘鍏?Processing锛屾垨鑰呭湪鏌愪簺寮傚父璺緞涓嬬洿鎺ユ爣澶辫触銆?
            .Permit(Trigger.StartProcessing, JobStatus.Processing)
            .Permit(Trigger.Fail, JobStatus.Failed)
            .OnEntry(() =>
            {
                // 鍥炲埌 Pending 鏃讹紝鎶婁笂涓€杞墽琛岀棔杩规竻鎺夈€?
                ErrorMessage = null;
                ResultJson = null;
                StartedAtUtc = null;
                CompletedAtUtc = null;
            });

        stateMachine.Configure(JobStatus.Processing)
            .OnEntry(() =>
            {
                // 鐪熸寮€濮嬫墽琛屾椂璁板綍寮€濮嬫椂闂达紝骞舵竻鐞嗘帀涔嬪墠鐨勭粨鏋?閿欒銆?
                StartedAtUtc = DateTime.UtcNow;
                CompletedAtUtc = null;
                ErrorMessage = null;
                ResultJson = null;
            })
            .Permit(Trigger.Succeed, JobStatus.Succeeded)
            .Permit(Trigger.Fail, JobStatus.Failed);

        stateMachine.Configure(JobStatus.Succeeded)
            .OnEntry(() =>
            {
                // 鎴愬姛缁堟€佸彧淇濈暀缁撴灉锛屼笉淇濈暀閿欒銆?
                CompletedAtUtc = DateTime.UtcNow;
                ErrorMessage = null;
            });

        stateMachine.Configure(JobStatus.Failed)
            .OnEntry(() =>
            {
                // 澶辫触鏃剁疮璁￠噸璇曟鏁帮紝骞舵妸缁撴灉娓呮帀銆?
                RetryCount++;
                ResultJson = null;
                CompletedAtUtc = DateTime.UtcNow;
            })
            .Permit(Trigger.Retry, JobStatus.Pending);

        stateMachine.OnUnhandledTrigger((state, trigger) =>
        {
            // 涓€鏃﹀彂鐢熼潪娉曠姸鎬佽烦杞紝鐩存帴鎶涘紓甯革紝澶栧眰涓嶈兘鍋峰伔缁曡繃鐘舵€佹満銆?
            throw new InvalidOperationException(CreateUnhandledTriggerMessage(state, trigger));
        });

        return stateMachine;
    }

    private static string CreateUnhandledTriggerMessage(JobStatus state, Trigger trigger)
    {
        // 缁欏灞傛洿鏄庣‘鐨勯敊璇俊鎭紝鏂逛究 API/鏃ュ織/娴嬭瘯鐞嗚В鍒板簳涓轰粈涔堥潪娉曘€?
        return (state, trigger) switch
        {
            (JobStatus.Pending, Trigger.Succeed) => "Only processing jobs can be marked as succeeded.",
            (JobStatus.Pending, Trigger.Retry) => "Only failed jobs can be retried.",
            (JobStatus.Processing, Trigger.StartProcessing) => "Only pending jobs can start processing.",
            (JobStatus.Processing, Trigger.Retry) => "Only failed jobs can be retried.",
            (JobStatus.Succeeded, Trigger.StartProcessing) => "Only pending jobs can start processing.",
            (JobStatus.Succeeded, Trigger.Succeed) => "Only processing jobs can be marked as succeeded.",
            (JobStatus.Succeeded, Trigger.Fail) => "Only pending or processing jobs can be marked as failed.",
            (JobStatus.Succeeded, Trigger.Retry) => "Only failed jobs can be retried.",
            (JobStatus.Failed, Trigger.StartProcessing) => "Only pending jobs can start processing.",
            (JobStatus.Failed, Trigger.Succeed) => "Only processing jobs can be marked as succeeded.",
            (JobStatus.Failed, Trigger.Fail) => "Only pending or processing jobs can be marked as failed.",
            _ => $"Trigger '{trigger}' is not valid for state '{state}'."
        };
    }

    private enum Trigger
    {
        // Trigger 浠ｈ〃鈥滀粈涔堝姩浣滆Е鍙戠姸鎬佹祦杞€濓紝鑰屼笉鏄姸鎬佹湰韬€?
        StartProcessing,
        Succeed,
        Fail,
        Retry
    }
}
