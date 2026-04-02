using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Abstractions.Storage;
using DocFlowCloud.Application.Exceptions;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Domain.Outbox;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocFlowCloud.Application.Jobs;

// 应用层服务：
// 负责组织“创建任务、查询任务、下载结果、业务级重试”等用例流程。
// 当前文档转 PDF 已改成“文件进入共享存储，数据库只存 storage key”。
public sealed class JobService
{
    public const string DocumentToPdfJobType = "DocumentToPdf";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ICorrelationContextAccessor _correlationContextAccessor;
    private readonly IFileStorage _fileStorage;
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<JobService> _logger;
    private readonly IOutboxMessageRepository _outboxMessageRepository;

    public JobService(
        ICorrelationContextAccessor correlationContextAccessor,
        IFileStorage fileStorage,
        IJobRepository jobRepository,
        ILogger<JobService> logger,
        IOutboxMessageRepository outboxMessageRepository)
    {
        _correlationContextAccessor = correlationContextAccessor;
        _fileStorage = fileStorage;
        _jobRepository = jobRepository;
        _logger = logger;
        _outboxMessageRepository = outboxMessageRepository;
    }

    public async Task<Guid> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        // 创建任务时，Job 和 Outbox 要一起写库，确保“业务记录”和“待发消息”一致。
        var job = new Job(request.Name, request.Type, request.PayloadJson);

        await _jobRepository.AddAsync(job, cancellationToken);
        await AddOutboxMessageAsync(job, _correlationContextAccessor.GetCorrelationId(), cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job created. JobId={JobId}, JobType={JobType}, JobName={JobName}, CorrelationId={CorrelationId}",
            job.Id,
            job.Type,
            job.Name,
            _correlationContextAccessor.GetCorrelationId());

        return job.Id;
    }

    public async Task<Guid> CreateDocumentToPdfAsync(
        string? name,
        string originalFileName,
        string contentType,
        byte[] fileBytes,
        CancellationToken cancellationToken = default)
    {
        // 创建阶段先把原文件写进存储层，业务表里只保存逻辑 storage key。
        var inputStorageKey = await _fileStorage.SaveAsync(
            "uploads",
            originalFileName,
            fileBytes,
            cancellationToken);

        var payload = new DocumentToPdfJobPayload
        {
            OriginalFileName = originalFileName,
            ContentType = contentType,
            InputStorageKey = inputStorageKey
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
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        return job is null ? null : Map(job);
    }

    public async Task<List<JobDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _jobRepository.GetAllAsync(cancellationToken);
        return jobs.Select(Map).ToList();
    }

    public async Task<JobResultFileDto?> GetResultFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 下载结果时，按 output storage key 去存储层读 PDF。
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        if (job is null ||
            job.Type != DocumentToPdfJobType ||
            job.Status != JobStatus.Succeeded ||
            string.IsNullOrWhiteSpace(job.ResultJson))
        {
            return null;
        }

        var result = JsonSerializer.Deserialize<DocumentToPdfJobResult>(job.ResultJson, JsonSerializerOptions);
        if (result is null || string.IsNullOrWhiteSpace(result.OutputStorageKey))
        {
            return null;
        }

        var content = await _fileStorage.ReadAsync(result.OutputStorageKey, cancellationToken);
        if (content is null)
        {
            return null;
        }

        return new JobResultFileDto
        {
            FileName = result.OutputFileName,
            ContentType = "application/pdf",
            Content = content
        };
    }

    public async Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, job.MarkProcessing);
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(Guid jobId, string resultJson, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkSucceeded(resultJson));
        await _jobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job succeeded. JobId={JobId}, JobType={JobType}, CorrelationId={CorrelationId}",
            job.Id,
            job.Type,
            _correlationContextAccessor.GetCorrelationId());
    }

    public async Task MarkFailedAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkFailed(errorMessage));
        await _jobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Job failed. JobId={JobId}, JobType={JobType}, CorrelationId={CorrelationId}, ErrorMessage={ErrorMessage}",
            job.Id,
            job.Type,
            _correlationContextAccessor.GetCorrelationId(),
            errorMessage);
    }

    public async Task RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // 业务级重试：先把状态从 Failed 拉回 Pending，再补一条新的 outbox 消息。
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, job.Retry);
        await AddOutboxMessageAsync(job, _correlationContextAccessor.GetCorrelationId(), cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job retried. JobId={JobId}, JobType={JobType}, RetryCount={RetryCount}, CorrelationId={CorrelationId}",
            job.Id,
            job.Type,
            job.RetryCount,
            _correlationContextAccessor.GetCorrelationId());
    }

    private async Task AddOutboxMessageAsync(Job job, string correlationId, CancellationToken cancellationToken)
    {
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
