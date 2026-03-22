using System.Text.Json;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Inbox;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Domain.Outbox;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocFlowCloud.Worker;

public sealed class StaleInboxRecoveryWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    // 当前恢复器只处理 Job consumer 的卡死消息。
    // Notification consumer 的处理逻辑较轻，先不做自动恢复编排。
    private const string ConsumerName = "DocFlowCloud.JobConsumer";
    private const int MaxRecoveryRetryCount = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<StaleInboxRecoveryWorker> _logger;

    public StaleInboxRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        RabbitMqSettings settings,
        ILogger<StaleInboxRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 这是一个定时恢复器：
        // 每隔一段时间扫描一次 Inbox，寻找长时间停在 Processing 的消息。
        _logger.LogInformation("Stale inbox recovery worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverStaleInboxesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while recovering stale inbox messages.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.StaleRecoveryScanSeconds), stoppingToken);
        }
    }

    private async Task RecoverStaleInboxesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 超过 ProcessingTimeoutSeconds 还没完成的 Inbox，认为已经 stale。
        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(-_settings.ProcessingTimeoutSeconds);

        // 这里只扫描 Job consumer 下超时的 Processing 记录。
        // 一次最多处理 20 条，避免恢复任务本身把数据库打得过重。
        var staleInboxIds = await dbContext.InboxMessages
            .Where(x =>
                x.ConsumerName == ConsumerName &&
                x.Status == InboxStatus.Processing &&
                x.ClaimedAtUtc <= staleBeforeUtc)
            .OrderBy(x => x.ClaimedAtUtc)
            .Select(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var inboxId in staleInboxIds)
        {
            await RecoverSingleInboxAsync(inboxId, cancellationToken);
        }
    }

    private async Task RecoverSingleInboxAsync(Guid inboxId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 单条恢复必须在事务里完成，保证 Job、Inbox、Outbox 三者状态同步。
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(x => x.Id == inboxId, cancellationToken);
        if (inbox is null ||
            inbox.ConsumerName != ConsumerName ||
            !inbox.IsStale(DateTime.UtcNow, TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds)))
        {
            // 重新读取后如果发现已经不 stale，就说明别的实例可能已经处理了，直接跳过。
            return;
        }

        // 通过原始 Outbox 消息找到当时触发这次处理的消息体，
        // 后面需要基于它构造一条新的 replay 消息。
        var originalOutbox = await dbContext.OutboxMessages
            .Where(x =>
                x.Type == nameof(JobCreatedIntegrationMessage) &&
                x.PayloadJson.Contains(inbox.MessageId.ToString()))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (originalOutbox is null)
        {
            // 找不到原始消息时，只能把当前 Inbox 收口为失败，避免永远卡在 Processing。
            inbox.MarkFailed("Processing timed out, but the original integration message could not be found.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because its original message could not be resolved.",
                inbox.Id);
            return;
        }

        var originalMessage = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(originalOutbox.PayloadJson, JsonSerializerOptions);
        if (originalMessage is null || originalMessage.MessageId != inbox.MessageId)
        {
            // 原始消息格式损坏时，不能盲目重放，先明确落成失败，等待人工处理。
            inbox.MarkFailed("Processing timed out, but the original integration message payload was invalid.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because its original message payload was invalid.",
                inbox.Id);
            return;
        }

        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == originalMessage.JobId, cancellationToken);
        if (job is null)
        {
            // Job 已不存在时，也不能继续恢复，只能把消息侧失败收口。
            inbox.MarkFailed("Processing timed out, but the related job could not be found.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale inbox {InboxId} was marked as failed because job {JobId} no longer exists.",
                inbox.Id,
                originalMessage.JobId);
            return;
        }

        if (job.Status == JobStatus.Succeeded)
        {
            // 如果业务其实已经成功，只是旧 Inbox 没来得及收尾，
            // 那就直接把 Inbox 补成 Processed，不再发 replay。
            inbox.MarkProcessed();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Stale inbox {InboxId} was completed because job {JobId} had already succeeded.",
                inbox.Id,
                job.Id);
            return;
        }

        // 卡死恢复也需要上限控制，否则同一条消息如果一直卡死，就会不断 replay。
        // 这里复用 Job.RetryCount 作为总重试计数，让“普通失败重试”和“卡死恢复重试”
        // 都能在同一个上限里被约束住。
        var projectedRetryCount = job.Status switch
        {
            JobStatus.Pending or JobStatus.Processing => job.RetryCount + 1,
            JobStatus.Failed => job.RetryCount,
            _ => job.RetryCount
        };

        if (projectedRetryCount >= MaxRecoveryRetryCount)
        {
            if (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
            {
                job.MarkFailed("Processing timed out. Recovery retry limit reached.");
            }

            inbox.MarkFailed("Processing timed out. Recovery retry limit reached.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Stale recovery retry limit reached for job {JobId}. InboxId: {InboxId}, RetryCount: {RetryCount}, Limit: {Limit}",
                job.Id,
                inbox.Id,
                projectedRetryCount,
                MaxRecoveryRetryCount);
            return;
        }

        if (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
        {
            // Job 还没真正完成时，先把旧处理明确标成失败，再走 Retry 回到 Pending。
            // 这样不会绕过状态机，也不会直接手改成某个终态。
            job.MarkFailed("Processing timed out. Recovery replay scheduled.");
            job.Retry();
        }
        else if (job.Status == JobStatus.Failed)
        {
            // 如果业务已经是 Failed，就直接走 Retry 回到 Pending。
            job.Retry();
        }

        // 旧 Inbox 这次恢复流程就收口为失败，避免继续占着 Processing。
        inbox.MarkFailed("Processing timed out. Recovery replay scheduled.");

        // 重新生成一条新的业务消息，让系统按正常主流程再跑一次。
        // 这里保留原来的 CorrelationId 和 IdempotencyKey，方便串联日志和保证业务幂等。
        var replayMessage = new JobCreatedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = job.Id,
            CorrelationId = originalMessage.CorrelationId,
            IdempotencyKey = originalMessage.IdempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        var replayPayload = JsonSerializer.Serialize(replayMessage, JsonSerializerOptions);
        dbContext.OutboxMessages.Add(new OutboxMessage(nameof(JobCreatedIntegrationMessage), replayPayload));

        // 事务提交后，OutboxPublisherWorker 会按原来的机制把 replay 消息发出去。
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogWarning(
            "Recovered stale inbox {InboxId} for job {JobId}. Recovery replay scheduled. RecoveryRetryCount: {RetryCount}",
            inbox.Id,
            job.Id,
            projectedRetryCount);
    }
}
