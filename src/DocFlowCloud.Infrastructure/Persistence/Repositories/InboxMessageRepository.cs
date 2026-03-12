using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Domain.Inbox;
using Microsoft.EntityFrameworkCore;

namespace DocFlowCloud.Infrastructure.Persistence.Repositories;

public sealed class InboxMessageRepository : IInboxMessageRepository
{
    private readonly AppDbContext _dbContext;

    public InboxMessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExistsAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InboxMessages
            .AnyAsync(x => x.MessageId == messageId && x.ConsumerName == consumerName, cancellationToken);
    }

    public async Task AddAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.InboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}