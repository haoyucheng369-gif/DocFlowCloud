using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Exceptions;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Domain.Outbox;
using System.Text.Json;

namespace DocFlowCloud.Application.Jobs;

// 应用层服务：负责把“创建任务、查询任务、业务级重试”这些用例串起来。
// 这里不直接关心 HTTP，也不直接关心 RabbitMQ 细节，只负责组织领域对象和持久化。
public sealed class JobService
{
    private readonly ICorrelationContextAccessor _correlationContextAccessor;
    private readonly IJobRepository _jobRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;

    public JobService(
        ICorrelationContextAccessor correlationContextAccessor,
        IJobRepository jobRepository,
        IOutboxMessageRepository outboxMessageRepository)
    {
        _correlationContextAccessor = correlationContextAccessor;
        _jobRepository = jobRepository;
        _outboxMessageRepository = outboxMessageRepository;
    }

    public async Task<Guid> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        // 创建任务时，同一个保存流程里会同时写入：
        // 1. Jobs 业务记录
        // 2. OutboxMessages 待发布消息
        // 这样就不会出现“Job 创建成功，但系统里完全没有消息记录”的情况。
        var job = new Job(request.Name, request.Type, request.PayloadJson);

        await _jobRepository.AddAsync(job, cancellationToken);
        await AddOutboxMessageAsync(job, _correlationContextAccessor.GetCorrelationId(), cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return job.Id;
    }

    public async Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 查询接口只返回 DTO，不直接把领域对象暴露给外层。
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return null;
        }

        return Map(job);
    }

    public async Task<List<JobDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // 列表查询只是读取，不参与状态流转。
        var jobs = await _jobRepository.GetAllAsync(cancellationToken);
        return jobs.Select(Map).ToList();
    }

    public async Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // 这类方法主要用于应用层显式推进状态；
        // 状态是否合法由领域状态机最终裁决。
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, job.MarkProcessing);
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(Guid jobId, string resultJson, CancellationToken cancellationToken = default)
    {
        // 成功推进状态前，仍然会经过领域层的状态机校验。
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkSucceeded(resultJson));
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken = default)
    {
        // 失败推进状态前，仍然会经过领域层的状态机校验。
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkFailed(errorMessage));
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // Retry 是业务级恢复入口：
        // 先把 Failed 的 Job 通过状态机回到 Pending，
        // 再补一条新的 Outbox 消息，让主流程重新跑一次。
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, job.Retry);
        await AddOutboxMessageAsync(job, _correlationContextAccessor.GetCorrelationId(), cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task AddOutboxMessageAsync(Job job, string correlationId, CancellationToken cancellationToken)
    {
        // 这里构造的集成消息就是后续 RabbitMQ 主流程真正要发送的内容。
        // IdempotencyKey 固定按 JobId 生成，确保重复消费时业务副作用仍可幂等。
        var integrationMessage = new JobCreatedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = job.Id,
            CorrelationId = correlationId,
            IdempotencyKey = $"job:{job.Id}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var payload = JsonSerializer.Serialize(integrationMessage);
        var outboxMessage = new OutboxMessage(nameof(JobCreatedIntegrationMessage), payload);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);
    }

    private static void TryChangeState(Guid jobId, Action action)
    {
        // 领域层状态机抛的是 InvalidOperationException，
        // 这里统一包装成应用层更明确的 InvalidJobStateException。
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidJobStateException(jobId, exception.Message, exception);
        }
    }

    private static JobDto Map(Job job)
    {
        // 领域对象 -> DTO 的单向映射，避免 API 层依赖领域内部结构。
        return new JobDto
        {
            Id = job.Id,
            Name = job.Name,
            Type = job.Type,
            Status = job.Status.ToString(),
            RetryCount = job.RetryCount,
            CreatedAtUtc = job.CreatedAtUtc,
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            ErrorMessage = job.ErrorMessage
        };
    }
}
