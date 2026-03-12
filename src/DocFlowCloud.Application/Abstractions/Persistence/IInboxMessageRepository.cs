using DocFlowCloud.Domain.Inbox;

namespace DocFlowCloud.Application.Abstractions.Persistence;

public interface IInboxMessageRepository
{
    Task<bool> ExistsAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default);
    Task AddAsync(InboxMessage message, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}