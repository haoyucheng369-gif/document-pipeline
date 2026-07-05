using Stateless;

namespace CloudDocumentPipeline.Domain.Jobs;

// Domain aggregate for an asynchronous processing job.
// State transitions are enforced here so API and workers cannot bypass lifecycle rules.
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
        // EF Core constructor. The state machine is rebuilt after materialization.
        _stateMachine = CreateStateMachine();
    }

    public Job(string name, string type, string payloadJson)
    {
        // New jobs always enter the pipeline as Pending and wait for a worker message.
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
        // Workers call this when they begin the external side effect.
        _stateMachine.Fire(Trigger.StartProcessing);
    }

    public void MarkSucceeded(string resultJson)
    {
        // Result JSON stores provider-agnostic metadata such as output storage keys.
        ResultJson = resultJson;
        _stateMachine.Fire(Trigger.Succeed);
    }

    public void MarkFailed(string errorMessage)
    {
        // Failure keeps a diagnostic message and moves the job to a retryable terminal state.
        ErrorMessage = errorMessage;
        _stateMachine.Fire(Trigger.Fail);
    }

    public void Retry()
    {
        // Only failed jobs can be moved back to Pending for replay.
        _stateMachine.Fire(Trigger.Retry);
    }

    private StateMachine<JobStatus, Trigger> CreateStateMachine()
    {
        var stateMachine = new StateMachine<JobStatus, Trigger>(
            () => Status,
            status => Status = status);

        stateMachine.Configure(JobStatus.Pending)
            .Permit(Trigger.StartProcessing, JobStatus.Processing)
            .Permit(Trigger.Fail, JobStatus.Failed)
            .OnEntry(() =>
            {
                // A retry starts with a clean execution surface.
                ErrorMessage = null;
                ResultJson = null;
                StartedAtUtc = null;
                CompletedAtUtc = null;
            });

        stateMachine.Configure(JobStatus.Processing)
            .OnEntry(() =>
            {
                // Processing timestamps measure worker execution time, not queue wait time.
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
                // Success is terminal for this aggregate.
                CompletedAtUtc = DateTime.UtcNow;
                ErrorMessage = null;
            });

        stateMachine.Configure(JobStatus.Failed)
            .OnEntry(() =>
            {
                // RetryCount counts failed processing attempts, including recovery failures.
                RetryCount++;
                ResultJson = null;
                CompletedAtUtc = DateTime.UtcNow;
            })
            .Permit(Trigger.Retry, JobStatus.Pending);

        stateMachine.OnUnhandledTrigger((state, trigger) =>
        {
            // Convert state-machine errors into business-readable messages for API responses.
            throw new InvalidOperationException(CreateUnhandledTriggerMessage(state, trigger));
        });

        return stateMachine;
    }

    private static string CreateUnhandledTriggerMessage(JobStatus state, Trigger trigger)
    {
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
        StartProcessing,
        Succeed,
        Fail,
        Retry
    }
}
