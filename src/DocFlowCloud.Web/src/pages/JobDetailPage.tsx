import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getJob, getResultFileUrl, retryJob } from "../lib/api";
import { formatDate } from "../lib/format";
import type { Job } from "../types";
import { StatusBadge } from "../components/StatusBadge";

// 任务详情页：查看单个任务的状态、结果和恢复操作。
export function JobDetailPage() {
  const { id = "" } = useParams();
  const [job, setJob] = useState<Job | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isRetrying, setIsRetrying] = useState(false);

  // 加载详情：进入页面时读取任务详情。
  useEffect(() => {
    let isDisposed = false;

    async function loadJob() {
      try {
        const response = await getJob(id);
        if (!isDisposed) {
          setJob(response);
          setError(null);
        }
      } catch (error) {
        if (!isDisposed) {
          setError(
            error instanceof Error ? error.message : "Failed to load the job."
          );
        }
      } finally {
        if (!isDisposed) {
          setIsLoading(false);
        }
      }
    }

    void loadJob();

    return () => {
      isDisposed = true;
    };
  }, [id]);

  // 轮询逻辑：任务仍在 Pending/Processing 时自动刷新，成功或失败后停止轮询。
  useEffect(() => {
    if (!job || (job.status !== "Pending" && job.status !== "Processing")) {
      return;
    }

    const timer = window.setInterval(async () => {
      try {
        const response = await getJob(id);
        setJob(response);
      } catch (error) {
        setError(
          error instanceof Error ? error.message : "Failed to load the job."
        );
      }
    }, 3000);

    return () => window.clearInterval(timer);
  }, [id, job]);

  // 重试逻辑：仅允许失败任务重新进入主流程。
  async function handleRetry() {
    if (!job) {
      return;
    }

    setIsRetrying(true);
    setError(null);

    try {
      await retryJob(job.id);
      const refreshed = await getJob(job.id);
      setJob(refreshed);
    } catch (error) {
      setError(error instanceof Error ? error.message : "Retry failed.");
    } finally {
      setIsRetrying(false);
    }
  }

  if (isLoading) {
    return <p className="text-sm text-slate-600">Loading...</p>;
  }

  if (!job) {
    return (
      <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        {error ?? "The requested job was not found."}
      </div>
    );
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1.2fr_0.8fr]">
      <section className="rounded-3xl border border-line bg-white p-8 shadow-sm">
        <div className="flex items-start justify-between gap-6">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
              Job Tracking
            </p>
            <h1 className="mt-2 text-3xl font-semibold tracking-tight text-ink">
              Job Detail
            </h1>
            <p className="mt-3 text-sm leading-7 text-slate-600">{job.name}</p>
          </div>

          <Link
            to="/jobs"
            className="rounded-full border border-line px-4 py-2 text-sm font-medium text-ink transition hover:bg-soft"
          >
            Back to Jobs
          </Link>
        </div>

        {error ? (
          <div className="mt-6 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        ) : null}

        <div className="mt-8 grid gap-5 rounded-3xl bg-soft p-6 md:grid-cols-2">
          <InfoRow label="Task Type" value={job.type} />
          <InfoRow label="Created At" value={formatDate(job.createdAtUtc)} />
          <InfoRow
            label="Started At"
            value={job.startedAtUtc ? formatDate(job.startedAtUtc) : "Not started"}
          />
          <InfoRow
            label="Completed At"
            value={
              job.completedAtUtc ? formatDate(job.completedAtUtc) : "Not completed"
            }
          />
        </div>

        <div className="mt-8 rounded-3xl border border-line p-6">
          <div className="flex items-center justify-between gap-4">
            <h2 className="text-lg font-semibold text-ink">Conversion Result</h2>
            <StatusBadge status={job.status} />
          </div>

          <div className="mt-4 space-y-3 text-sm leading-7 text-slate-600">
            {job.status === "Succeeded" ? (
              <p>
                The conversion is complete. You can download the generated PDF.
              </p>
            ) : null}

            {job.status === "Failed" ? (
              <p>{job.errorMessage || "The job failed."}</p>
            ) : null}

            {(job.status === "Pending" || job.status === "Processing") ? (
              <p>
                The job is still running. This page refreshes automatically.
              </p>
            ) : null}

            <p>
              Original file details are kept in the backend job payload and
              processed asynchronously.
            </p>
          </div>
        </div>
      </section>

      <aside className="rounded-3xl border border-line bg-white p-8 shadow-sm">
        <h2 className="text-lg font-semibold text-ink">Actions</h2>

        <div className="mt-5 space-y-4 text-sm text-slate-600">
          <p>
            Current Status: <span className="font-medium text-ink">{job.status}</span>
          </p>
          <p>
            Retry Count:{" "}
            <span className="font-medium text-ink">{job.retryCount}</span>
          </p>
          <p>
            Correlation ID:{" "}
            <span className="break-all font-mono text-xs text-slate-500">
              {job.correlationId || "Unavailable"}
            </span>
          </p>
        </div>

        <div className="mt-8 space-y-3">
          {job.status === "Succeeded" ? (
            <a
              href={getResultFileUrl(job.id)}
              className="inline-flex w-full items-center justify-center rounded-full bg-accent px-5 py-3 text-sm font-semibold text-white transition hover:bg-accent/90"
            >
              Download Converted PDF
            </a>
          ) : null}

          {job.status === "Failed" ? (
            <button
              type="button"
              disabled={isRetrying}
              onClick={handleRetry}
              className="inline-flex w-full items-center justify-center rounded-full border border-line px-5 py-3 text-sm font-semibold text-ink transition hover:bg-soft disabled:cursor-not-allowed disabled:text-slate-400"
            >
              {isRetrying ? "Retrying..." : "Retry Failed Job"}
            </button>
          ) : null}
        </div>
      </aside>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">
        {label}
      </dt>
      <dd className="mt-2 text-sm font-medium text-ink">{value}</dd>
    </div>
  );
}
