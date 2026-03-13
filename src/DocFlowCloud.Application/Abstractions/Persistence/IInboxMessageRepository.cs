using DocFlowCloud.Domain.Inbox;

namespace DocFlowCloud.Application.Abstractions.Persistence;

public interface IInboxMessageRepository
{
    // 尝试先写入一条 Processing 记录，依靠唯一键抢占这条消息的处理权。
    // 如果历史记录是 Failed，或者 Processing 已超时，则允许重新 claim。
    // 返回 true 表示当前实例抢占/接管成功，返回 false 表示别的实例仍在处理或已处理完成。
    Task<bool> TryClaimAsync(Guid messageId, string consumerName, TimeSpan processingTimeout, CancellationToken cancellationToken = default);

    // 根据消息标识取回 inbox 记录，用于后续把状态改成 Processed 或 Failed。
    Task<InboxMessage?> GetByMessageIdAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
