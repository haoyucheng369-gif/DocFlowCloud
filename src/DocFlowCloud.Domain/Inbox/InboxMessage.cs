using Stateless;

namespace DocFlowCloud.Domain.Inbox;

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
        _stateMachine = CreateStateMachine();
    }

    public InboxMessage(Guid messageId, string consumerName)
    {
        Id = Guid.NewGuid();
        MessageId = messageId;
        ConsumerName = consumerName;
        Status = InboxStatus.Processing;
        ClaimedAtUtc = DateTime.UtcNow;
        _stateMachine = CreateStateMachine();
    }

    public bool IsStale(DateTime utcNow, TimeSpan processingTimeout)
    {
        return Status == InboxStatus.Processing && ClaimedAtUtc.Add(processingTimeout) <= utcNow;
    }

    public void Reclaim()
    {
        _stateMachine.Fire(Trigger.Reclaim);
    }

    public void MarkProcessed()
    {
        _stateMachine.Fire(Trigger.Complete);
    }

    public void MarkFailed(string errorMessage)
    {
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
                ClaimedAtUtc = DateTime.UtcNow;
                ProcessedAtUtc = null;
                ErrorMessage = null;
            });

        stateMachine.Configure(InboxStatus.Processed)
            .OnEntry(() =>
            {
                ProcessedAtUtc = DateTime.UtcNow;
                ErrorMessage = null;
            });

        stateMachine.Configure(InboxStatus.Failed)
            .OnEntry(() =>
            {
                ProcessedAtUtc = DateTime.UtcNow;
            })
            .Permit(Trigger.Reclaim, InboxStatus.Processing);

        stateMachine.OnUnhandledTrigger((state, trigger) =>
        {
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
