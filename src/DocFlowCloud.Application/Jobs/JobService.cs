using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Exceptions;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Domain.Outbox;
using System.Text.Json;

namespace DocFlowCloud.Application.Jobs;

// 应用层服务：
// 负责把“创建任务、查询任务、结果下载、业务级重试”这些用例串起来。
// 这里不直接关心 HTTP 和 RabbitMQ 细节，只组织领域对象与持久化流程。
public sealed class JobService
{
    // 简单文档转 PDF 任务当前统一使用这个类型名，方便 API 和 Worker 对齐。
    public const string DocumentToPdfJobType = "DocumentToPdf";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
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
        // 通用任务创建流程：
        // 1. 写入 Job 业务记录
        // 2. 补一条 Outbox 待发布消息
        // 这样可以保证“业务已落库”和“消息最终会发出”两件事保持一致。
        var job = new Job(request.Name, request.Type, request.PayloadJson);

        await _jobRepository.AddAsync(job, cancellationToken);
        await AddOutboxMessageAsync(job, _correlationContextAccessor.GetCorrelationId(), cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return job.Id;
    }

    public async Task<Guid> CreateDocumentToPdfAsync(
        string? name,
        string originalFileName,
        string contentType,
        byte[] fileBytes,
        CancellationToken cancellationToken = default)
    {
        // 文档转 PDF 的最小可用版本：
        // 当前支持 image、txt、md、html 四类输入，并把文件内容直接放进 payloadJson，
        // 这样本地调试和容器运行都不需要先引入共享文件存储。
        var payload = new DocumentToPdfJobPayload
        {
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileBytesBase64 = Convert.ToBase64String(fileBytes)
        };

        var request = new CreateJobRequest
        {
            Name = string.IsNullOrWhiteSpace(name) ? originalFileName : name,
            Type = DocumentToPdfJobType,
            PayloadJson = JsonSerializer.Serialize(payload, JsonSerializerOptions)
        };

        return await CreateAsync(request, cancellationToken);
    }

    public async Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 查询单个任务时，只返回 DTO，不把领域对象直接暴露到 API 层。
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        return job is null ? null : Map(job);
    }

    public async Task<List<JobDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // 列表查询只是读取，不参与状态变更。
        var jobs = await _jobRepository.GetAllAsync(cancellationToken);
        return jobs.Select(Map).ToList();
    }

    public async Task<JobResultFileDto?> GetResultFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 结果下载只在 DocumentToPdf 成功任务上生效。
        // 如果任务还没成功、类型不对或结果 JSON 缺失，就返回 null。
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        if (job is null ||
            job.Type != DocumentToPdfJobType ||
            job.Status != JobStatus.Succeeded ||
            string.IsNullOrWhiteSpace(job.ResultJson))
        {
            return null;
        }

        var result = JsonSerializer.Deserialize<DocumentToPdfJobResult>(job.ResultJson, JsonSerializerOptions);
        if (result is null || string.IsNullOrWhiteSpace(result.PdfBytesBase64))
        {
            return null;
        }

        return new JobResultFileDto
        {
            FileName = result.OutputFileName,
            ContentType = "application/pdf",
            Content = Convert.FromBase64String(result.PdfBytesBase64)
        };
    }

    public async Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // 这些状态推进方法主要给应用层显式调用，
        // 状态是否合法最终仍由领域状态机裁决。
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, job.MarkProcessing);
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(Guid jobId, string resultJson, CancellationToken cancellationToken = default)
    {
        // 成功推进前先经过状态机校验，避免非法跳状态。
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkSucceeded(resultJson));
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken = default)
    {
        // 失败推进前同样要经过状态机校验。
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkFailed(errorMessage));
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // 业务级重试入口：
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
        // 这里构造的是系统内部真正要发布到 RabbitMQ 的集成消息。
        // 幂等键固定按 JobId 生成，保证重复消费时业务副作用仍可幂等。
        var integrationMessage = new JobCreatedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = job.Id,
            CorrelationId = correlationId,
            IdempotencyKey = $"job:{job.Id}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var payload = JsonSerializer.Serialize(integrationMessage, JsonSerializerOptions);
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
        // 统一做领域对象 -> DTO 映射，避免 API 直接依赖领域内部结构。
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
