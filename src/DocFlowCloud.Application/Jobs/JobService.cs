using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Exceptions;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Domain.Outbox;
using System.Text.Json;

namespace DocFlowCloud.Application.Jobs;

public sealed class JobService
{
    private readonly IJobRepository _jobRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;

    public JobService(
        IJobRepository jobRepository,
        IOutboxMessageRepository outboxMessageRepository)
    {
        _jobRepository = jobRepository;
        _outboxMessageRepository = outboxMessageRepository;
    }

    public async Task<Guid> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        var job = new Job(request.Name, request.Type, request.PayloadJson);

        await _jobRepository.AddAsync(job, cancellationToken);

        var integrationMessage = new JobCreatedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = job.Id,
            IdempotencyKey = $"job:{job.Id}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var payload = JsonSerializer.Serialize(integrationMessage);
        var outboxMessage = new OutboxMessage(nameof(JobCreatedIntegrationMessage), payload);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return job.Id;
    }

    public async Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return null;
        }

        return Map(job);
    }

    public async Task<List<JobDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _jobRepository.GetAllAsync(cancellationToken);
        return jobs.Select(Map).ToList();
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
    }

    public async Task MarkFailedAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkFailed(errorMessage));
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, job.Retry);

        var integrationMessage = new JobCreatedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = job.Id,
            IdempotencyKey = $"job:{job.Id}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var payload = JsonSerializer.Serialize(integrationMessage);
        var outboxMessage = new OutboxMessage(nameof(JobCreatedIntegrationMessage), payload);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);
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
