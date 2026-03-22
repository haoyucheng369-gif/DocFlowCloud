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

    // 这里的 claim 不是“声明”，而是“抢占这条消息的处理权”。
    // 依赖 (MessageId, ConsumerName) 唯一键，只有一个实例能首次插入成功。
    // 如果旧记录已经 Failed，或 Processing 超时了，则允许当前实例重新接管。
    public async Task<bool> TryClaimAsync(
        Guid messageId,
        string consumerName,
        TimeSpan processingTimeout,
        CancellationToken cancellationToken = default)
    {
        // 默认先尝试插入一条新的 Inbox 记录，初始状态是 Processing。
        var inboxMessage = new InboxMessage(messageId, consumerName);
        await _dbContext.InboxMessages.AddAsync(inboxMessage, cancellationToken);

        try
        {
            // 插入成功，说明当前实例抢到了处理权。
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // 唯一键冲突表示同一个 consumer 已经见过这条消息了。
            // 这里不直接失败，而是继续判断：是已处理、处理中，还是可以重新接管。
            _dbContext.Entry(inboxMessage).State = EntityState.Detached;

            var existingInbox = await GetByMessageIdAsync(messageId, consumerName, cancellationToken);
            if (existingInbox is null)
            {
                return false;
            }

            if (existingInbox.Status == InboxStatus.Processed)
            {
                // 已成功处理过的消息，不再重复执行。
                return false;
            }

            if (existingInbox.Status == InboxStatus.Failed ||
                existingInbox.IsStale(DateTime.UtcNow, processingTimeout))
            {
                // 失败消息，或处理超时卡住的消息，允许重新 claim 接管。
                existingInbox.Reclaim();
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            // 其余情况通常是“别人正在正常处理”，当前实例放弃。
            return false;
        }
    }

    // 读取已 claim 的 Inbox 记录，后续在事务里把它更新成 Processed / Failed。
    public async Task<InboxMessage?> GetByMessageIdAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
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
        // 不同数据库提供程序的唯一键异常码不同，这里统一封装成一个判断。
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
