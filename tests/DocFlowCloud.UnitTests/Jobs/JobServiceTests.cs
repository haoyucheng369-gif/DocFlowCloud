using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Exceptions;
using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Domain.Outbox;

namespace DocFlowCloud.UnitTests.Jobs;

public sealed class JobServiceTests
{
    [Fact]
    public async Task RetryAsync_WhenJobMissing_ThrowsJobNotFoundException()
    {
        var service = new JobService(new InMemoryJobRepository(), new InMemoryOutboxRepository());

        await Assert.ThrowsAsync<JobNotFoundException>(() => service.RetryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RetryAsync_WhenJobIsNotFailed_ThrowsInvalidJobStateException()
    {
        var repository = new InMemoryJobRepository();
        var job = new Job("demo", "pdf", "{}");
        await repository.AddAsync(job);

        var service = new JobService(repository, new InMemoryOutboxRepository());

        await Assert.ThrowsAsync<InvalidJobStateException>(() => service.RetryAsync(job.Id));
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
}
