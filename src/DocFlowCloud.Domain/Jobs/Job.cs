using Stateless;

namespace DocFlowCloud.Domain.Jobs;

// 领域聚合：表示一个异步业务任务。
// 这个类的核心职责不是“存数据”，而是约束任务状态只能按合法顺序流转。
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
        // 供 EF Core 反射构造使用。
        _stateMachine = CreateStateMachine();
    }

    public Job(string name, string type, string payloadJson)
    {
        // 新任务创建时默认从 Pending 开始。
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
        // 进入处理中。
        _stateMachine.Fire(Trigger.StartProcessing);
    }

    public void MarkSucceeded(string resultJson)
    {
        // 先保存结果，再推进状态。
        ResultJson = resultJson;
        _stateMachine.Fire(Trigger.Succeed);
    }

    public void MarkFailed(string errorMessage)
    {
        // 先记录错误，再推进状态。
        ErrorMessage = errorMessage;
        _stateMachine.Fire(Trigger.Fail);
    }

    public void Retry()
    {
        // 仅允许 Failed -> Pending。
        _stateMachine.Fire(Trigger.Retry);
    }

    private StateMachine<JobStatus, Trigger> CreateStateMachine()
    {
        // 状态机统一定义哪些状态可以如何转换，以及进入状态时要做什么副作用。
        var stateMachine = new StateMachine<JobStatus, Trigger>(
            () => Status,
            status => Status = status);

        stateMachine.Configure(JobStatus.Pending)
            // 新建或重试后的任务，下一步只能进入 Processing，或者在某些异常路径下直接标失败。
            .Permit(Trigger.StartProcessing, JobStatus.Processing)
            .Permit(Trigger.Fail, JobStatus.Failed)
            .OnEntry(() =>
            {
                // 回到 Pending 时，把上一轮执行痕迹清掉。
                ErrorMessage = null;
                ResultJson = null;
                StartedAtUtc = null;
                CompletedAtUtc = null;
            });

        stateMachine.Configure(JobStatus.Processing)
            .OnEntry(() =>
            {
                // 真正开始执行时记录开始时间，并清理掉之前的结果/错误。
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
                // 成功终态只保留结果，不保留错误。
                CompletedAtUtc = DateTime.UtcNow;
                ErrorMessage = null;
            });

        stateMachine.Configure(JobStatus.Failed)
            .OnEntry(() =>
            {
                // 失败时累计重试次数，并把结果清掉。
                RetryCount++;
                ResultJson = null;
                CompletedAtUtc = DateTime.UtcNow;
            })
            .Permit(Trigger.Retry, JobStatus.Pending);

        stateMachine.OnUnhandledTrigger((state, trigger) =>
        {
            // 一旦发生非法状态跳转，直接抛异常，外层不能偷偷绕过状态机。
            throw new InvalidOperationException(CreateUnhandledTriggerMessage(state, trigger));
        });

        return stateMachine;
    }

    private static string CreateUnhandledTriggerMessage(JobStatus state, Trigger trigger)
    {
        // 给外层更明确的错误信息，方便 API/日志/测试理解到底为什么非法。
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
        // Trigger 代表“什么动作触发状态流转”，而不是状态本身。
        StartProcessing,
        Succeed,
        Fail,
        Retry
    }
}
