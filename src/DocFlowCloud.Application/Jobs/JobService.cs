using DocFlowCloud.Application.Abstractions.Messaging;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Domain.Jobs;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocFlowCloud.Application.Jobs
{
    public class JobService
    {
        private readonly IJobRepository _jobRepository;
        private readonly IJobMessagePublisher _jobMessagePublisher;

        public JobService(IJobRepository jobRepository, IJobMessagePublisher jobMessagePublisher)
        {
            _jobMessagePublisher = jobMessagePublisher;
            _jobRepository = jobRepository; 
        }

        public async Task<Guid> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
        {
            var job = new Job(request.Name, request.Type, request.PayloadJson);

            await _jobRepository.AddAsync(job, cancellationToken);
            await _jobRepository.SaveChangesAsync(cancellationToken);

            await _jobMessagePublisher.PublishJobCreatedAsync(job.Id, cancellationToken);

            return job.Id;
        }

        public async Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
            if (job is null) return null;

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
                      ?? throw new InvalidOperationException($"Job '{jobId}' not found.");

            job.MarkProcessing();
            await _jobRepository.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkSucceededAsync(Guid jobId, string resultJson, CancellationToken cancellationToken = default)
        {
            var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
                      ?? throw new InvalidOperationException($"Job '{jobId}' not found.");

            job.MarkSucceeded(resultJson);
            await _jobRepository.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkFailedAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken = default)
        {
            var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
                      ?? throw new InvalidOperationException($"Job '{jobId}' not found.");

            job.MarkFailed(errorMessage);
            await _jobRepository.SaveChangesAsync(cancellationToken);
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
}
