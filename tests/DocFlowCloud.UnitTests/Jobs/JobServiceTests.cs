using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Exceptions;
using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Application.Messaging;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Domain.Outbox;
using System.Text.Json;

namespace DocFlowCloud.UnitTests.Jobs;

public sealed class JobServiceTests
{
    [Fact]
    public async Task RetryAsync_WhenJobMissing_ThrowsJobNotFoundException()
    {
        var service = new JobService(new StubCorrelationContextAccessor(), new InMemoryJobRepository(), new InMemoryOutboxRepository());

        await Assert.ThrowsAsync<JobNotFoundException>(() => service.RetryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RetryAsync_WhenJobIsNotFailed_ThrowsInvalidJobStateException()
    {
        var repository = new InMemoryJobRepository();
        var job = new Job("demo", "pdf", "{}");
        await repository.AddAsync(job);

        var service = new JobService(new StubCorrelationContextAccessor(), repository, new InMemoryOutboxRepository());

        await Assert.ThrowsAsync<InvalidJobStateException>(() => service.RetryAsync(job.Id));
    }

    [Fact]
    public async Task CreateAsync_UsesStableJobBasedIdempotencyKey()
    {
        var repository = new InMemoryJobRepository();
        var outboxRepository = new RecordingOutboxRepository();
        var service = new JobService(new StubCorrelationContextAccessor(), repository, outboxRepository);

        var jobId = await service.CreateAsync(new CreateJobRequest
        {
            Name = "demo",
            Type = "pdf",
            PayloadJson = "{}"
        });

        var payload = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(outboxRepository.SinglePayload)!;

        Assert.Equal(jobId, payload.JobId);
        Assert.Equal($"job:{jobId}", payload.IdempotencyKey);
        Assert.Equal("corr-123", payload.CorrelationId);
    }

    private sealed class InMemoryJobRepository : IJobRepository
    {
        private readonly List<Job> _jobs = [];

        public Task AddAsync(Job job, CancellationToken cancellationToken = default)
        {
            _jobs.Add(job);
            return Task.CompletedTask;
        }

        public Task<List<Job>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_jobs.OrderByDescending(x => x.CreatedAtUtc).ToList());
        }

        public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_jobs.FirstOrDefault(x => x.Id == id));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubCorrelationContextAccessor : ICorrelationContextAccessor
    {
        public string GetCorrelationId()
        {
            return "corr-123";
        }
    }

    private sealed class InMemoryOutboxRepository : IOutboxMessageRepository
    {
        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<List<OutboxMessage>> GetUnprocessedAsync(int take, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<OutboxMessage>());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRepository : IOutboxMessageRepository
    {
        public string SinglePayload { get; private set; } = string.Empty;

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            SinglePayload = message.PayloadJson;
            return Task.CompletedTask;
        }

        public Task<List<OutboxMessage>> GetUnprocessedAsync(int take, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<OutboxMessage>());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
