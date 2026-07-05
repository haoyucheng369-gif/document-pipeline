using Stateless;

namespace CloudDocumentPipeline.Domain.Inbox;

// Tracks whether a specific consumer has already handled a broker message.
// This lets consumers retry safely without repeating side effects after completion.
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
        // EF Core constructor. The state machine is rebuilt after materialization.
        _stateMachine = CreateStateMachine();
    }

    public InboxMessage(Guid messageId, string consumerName)
    {
        // Creating an inbox row is the claim operation; new claims start as Processing.
        Id = Guid.NewGuid();
        MessageId = messageId;
        ConsumerName = consumerName;
        Status = InboxStatus.Processing;
        ClaimedAtUtc = DateTime.UtcNow;
        _stateMachine = CreateStateMachine();
    }

    public bool IsStale(DateTime utcNow, TimeSpan processingTimeout)
    {
        // Stale processing means the consumer claimed the message but did not finish in time.
        return Status == InboxStatus.Processing && ClaimedAtUtc.Add(processingTimeout) <= utcNow;
    }

    public void Reclaim()
    {
        // Reclaim allows failed or timed-out processing to be taken over by another worker.
        _stateMachine.Fire(Trigger.Reclaim);
    }

    public void MarkProcessed()
    {
        // Processed is final for the same message and consumer pair.
        _stateMachine.Fire(Trigger.Complete);
    }

    public void MarkFailed(string errorMessage)
    {
        // Failed messages can be reclaimed later by retry or stale recovery logic.
        ErrorMessage = errorMessage;
        _stateMachine.Fire(Trigger.Fail);
    }

    private StateMachine<InboxStatus, Trigger> CreateStateMachine()
    {
        var stateMachine = new StateMachine<InboxStatus, Trigger>(
            () => Status,
            status => Status = status);

        stateMachine.Configure(InboxStatus.Processing)
            .Permit(Trigger.Complete, InboxStatus.Processed)
            .Permit(Trigger.Fail, InboxStatus.Failed)
            .PermitReentry(Trigger.Reclaim)
            .OnEntry(() =>
            {
                // Reclaim refreshes ownership and clears prior failure details.
                ClaimedAtUtc = DateTime.UtcNow;
                ProcessedAtUtc = null;
                ErrorMessage = null;
            });

        stateMachine.Configure(InboxStatus.Processed)
            .OnEntry(() =>
            {
                // ProcessedAtUtc records when this consumer finished the message.
                ProcessedAtUtc = DateTime.UtcNow;
                ErrorMessage = null;
            });

        stateMachine.Configure(InboxStatus.Failed)
            .OnEntry(() =>
            {
                // Failed inbox rows are closed for this attempt but remain reclaimable.
                ProcessedAtUtc = DateTime.UtcNow;
            })
            .Permit(Trigger.Reclaim, InboxStatus.Processing);

        stateMachine.OnUnhandledTrigger((state, trigger) =>
        {
            // Keep illegal consumer transitions explicit instead of silently ignoring duplicates.
            throw new InvalidOperationException(CreateUnhandledTriggerMessage(state, trigger));
        });

        return stateMachine;
    }

    private static string CreateUnhandledTriggerMessage(InboxStatus state, Trigger trigger)
    {
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
        Complete,
        Fail,
        Reclaim
    }
}
