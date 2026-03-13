using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Domain.Inbox;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DocFlowCloud.Infrastructure.Persistence.Repositories;

public sealed class InboxMessageRepository : IInboxMessageRepository
{
    private readonly AppDbContext _dbContext;

    public InboxMessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // 这里的 claim 不是“声明”，而是“抢占处理权”。
    // 多个实例同时消费同一条消息时，只有一个实例能成功插入唯一键记录。
    // 如果旧记录是 Failed，或 Processing 已超时，则允许当前实例重新接管。
    public async Task<bool> TryClaimAsync(
        Guid messageId,
        string consumerName,
        TimeSpan processingTimeout,
        CancellationToken cancellationToken = default)
    {
        var inboxMessage = new InboxMessage(messageId, consumerName);
        await _dbContext.InboxMessages.AddAsync(inboxMessage, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.Entry(inboxMessage).State = EntityState.Detached;

            var existingInbox = await GetByMessageIdAsync(messageId, consumerName, cancellationToken);
            if (existingInbox is null)
            {
                return false;
            }

            if (existingInbox.Status == InboxStatus.Processed)
            {
                return false;
            }

            if (existingInbox.Status == InboxStatus.Failed ||
                existingInbox.IsStale(DateTime.UtcNow, processingTimeout))
            {
                existingInbox.Reclaim();
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            return false;
        }
    }

    // 读取已经 claim 过的 inbox 记录，后续在事务里更新最终状态。
    public async Task<InboxMessage?> GetByMessageIdAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InboxMessages
            .FirstOrDefaultAsync(
                x => x.MessageId == messageId && x.ConsumerName == consumerName,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        if (exception.InnerException is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode == 19;
        }

        return false;
    }
}
