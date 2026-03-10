using System;
using System.Collections.Generic;
using System.Text;

namespace DocFlowCloud.Domain.Jobs
{
    public class Job
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = default!;
        public string Type { get; private set; } = default!;
        public JobStatus Status { get; private set; }
        public string PayloadJson { get; private set; } = default!;
        public string? ResultJson { get; private set; }
        public string? ErrorMessage { get; private set; }
        public int RetryCount { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime? StartedAtUtc { get; private set; }
        public DateTime? CompletedAtUtc { get; private set; }

        private Job() { }

        public Job(string name, string type, string payloadJson)
        {
            Id = Guid.NewGuid();
            Name = name;
            Type = type;
            PayloadJson = payloadJson;
            Status = JobStatus.Pending;
            RetryCount = 0;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public void MarkProcessing()
        {
            Status = JobStatus.Processing;
            StartedAtUtc = DateTime.UtcNow;
            ErrorMessage = null;
        }

        public void MarkSucceeded(string resultJson)
        {
            Status = JobStatus.Succeeded;
            ResultJson = resultJson;
            CompletedAtUtc = DateTime.UtcNow;
            ErrorMessage = null;
        }

        public void MarkFailed(string errorMessage)
        {
            Status = JobStatus.Failed;
            ErrorMessage = errorMessage;
            RetryCount++;
            CompletedAtUtc = DateTime.UtcNow;
        }
    }
}