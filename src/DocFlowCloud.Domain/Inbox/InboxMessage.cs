using Stateless;

namespace DocFlowCloud.Domain.Inbox;

// Inbox 记录的是“某个 consumer 如何处理某条消息”，
// 核心目标是去重、抢占处理权、跟踪处理中/已完成/失败状态。
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
        // 供 EF Core 反射构造使用。
        _stateMachine = CreateStateMachine();
    }

    public InboxMessage(Guid messageId, string consumerName)
    {
        // 成功 claim 一条消息时，会新建一条 Inbox 记录并直接进入 Processing。
        Id = Guid.NewGuid();
        MessageId = messageId;
        ConsumerName = consumerName;
        Status = InboxStatus.Processing;
        ClaimedAtUtc = DateTime.UtcNow;
        _stateMachine = CreateStateMachine();
    }

    public bool IsStale(DateTime utcNow, TimeSpan processingTimeout)
    {
        // 如果某条消息长期停在 Processing，可以认为旧处理已经卡死，允许后续接管。
        return Status == InboxStatus.Processing && ClaimedAtUtc.Add(processingTimeout) <= utcNow;
    }

    public void Reclaim()
    {
        // 重新 claim 本质上是“重新开始当前这条消息的处理权生命周期”。
        _stateMachine.Fire(Trigger.Reclaim);
    }

    public void MarkProcessed()
    {
        // 当前 consumer 已完成这条消息。
        _stateMachine.Fire(Trigger.Complete);
    }

    public void MarkFailed(string errorMessage)
    {
        // 当前 consumer 这次处理失败，记录错误并转入 Failed。
        ErrorMessage = errorMessage;
        _stateMachine.Fire(Trigger.Fail);
    }

    private StateMachine<InboxStatus, Trigger> CreateStateMachine()
    {
        // Inbox 状态机是消息消费层的保护，不是业务层状态机。
        var stateMachine = new StateMachine<InboxStatus, Trigger>(
            () => Status,
            status => Status = status);

        stateMachine.Configure(InboxStatus.Processing)
            // 正在处理时，只允许结束成功、结束失败，或者被重新接管。
            .Permit(Trigger.Complete, InboxStatus.Processed)
            .Permit(Trigger.Fail, InboxStatus.Failed)
            .PermitReentry(Trigger.Reclaim)
            .OnEntry(() =>
            {
                // 每次进入 Processing（首次 claim 或 reclaim）都刷新 claim 时间。
                ClaimedAtUtc = DateTime.UtcNow;
                ProcessedAtUtc = null;
                ErrorMessage = null;
            });

        stateMachine.Configure(InboxStatus.Processed)
            .OnEntry(() =>
            {
                // 成功处理后记录完成时间。
                ProcessedAtUtc = DateTime.UtcNow;
                ErrorMessage = null;
            });

        stateMachine.Configure(InboxStatus.Failed)
            .OnEntry(() =>
            {
                // 失败也算一种“已结束”，所以同样记录结束时间。
                ProcessedAtUtc = DateTime.UtcNow;
            })
            .Permit(Trigger.Reclaim, InboxStatus.Processing);

        stateMachine.OnUnhandledTrigger((state, trigger) =>
        {
            // 非法消息状态流转直接抛异常，避免被静默忽略。
            throw new InvalidOperationException(CreateUnhandledTriggerMessage(state, trigger));
        });

        return stateMachine;
    }

    private static string CreateUnhandledTriggerMessage(InboxStatus state, Trigger trigger)
    {
        // 提供更好理解的错误文本，方便恢复逻辑与测试排查。
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
        // Trigger 代表“消息消费流程里发生了什么动作”。
        Complete,
        Fail,
        Reclaim
    }
}
