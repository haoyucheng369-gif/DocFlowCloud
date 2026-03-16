export type JobStatus = "Pending" | "Processing" | "Succeeded" | "Failed";

export type Job = {
  id: string;
  name: string;
  type: string;
  status: JobStatus | string;
  retryCount: number;
  createdAtUtc: string;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  errorMessage?: string | null;
};

export type CreateJobResponse = {
  jobId: string;
  correlationId: string;
};
