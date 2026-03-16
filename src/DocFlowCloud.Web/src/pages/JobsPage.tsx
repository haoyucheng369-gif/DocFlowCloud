import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getJobs } from "../lib/api";
import { formatDate } from "../lib/format";
import type { Job } from "../types";
import { StatusBadge } from "../components/StatusBadge";

// 任务列表页：展示当前所有任务，方便快速查看处理状态和进入详情页。
export function JobsPage() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // 首次进入页面时拉取任务列表。
  useEffect(() => {
    async function loadJobs() {
      try {
        const response = await getJobs();
        setJobs(response);
      } catch {
        setError("Failed to load jobs.");
      } finally {
        setIsLoading(false);
      }
    }

    void loadJobs();
  }, []);

  return (
    <section className="rounded-3xl border border-line bg-white p-8 shadow-sm">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
            Monitoring
          </p>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight text-ink">
            Job List
          </h1>
        </div>

        <Link
          to="/"
          className="rounded-full border border-line px-5 py-3 text-sm font-medium text-ink transition hover:bg-soft"
        >
          New Task
        </Link>
      </div>

      {isLoading ? <p className="mt-8 text-sm text-slate-600">Loading...</p> : null}

      {error ? (
        <div className="mt-8 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}

      {!isLoading && !error ? (
        jobs.length > 0 ? (
          <div className="mt-8 overflow-hidden rounded-2xl border border-line">
            <table className="min-w-full divide-y divide-line text-sm">
              <thead className="bg-soft text-left text-slate-500">
                <tr>
                  <th className="px-5 py-4 font-medium">Task Name</th>
                  <th className="px-5 py-4 font-medium">Status</th>
                  <th className="px-5 py-4 font-medium">Created At</th>
                  <th className="px-5 py-4 font-medium">Retryable</th>
                  <th className="px-5 py-4 font-medium">Details</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line bg-white">
                {jobs.map((job) => (
                  <tr key={job.id} className="align-middle">
                    <td className="px-5 py-4 font-medium text-ink">{job.name}</td>
                    <td className="px-5 py-4">
                      <StatusBadge status={job.status} />
                    </td>
                    <td className="px-5 py-4 text-slate-600">
                      {formatDate(job.createdAtUtc)}
                    </td>
                    <td className="px-5 py-4 text-slate-600">
                      {job.status === "Failed" ? "Yes" : "No"}
                    </td>
                    <td className="px-5 py-4">
                      <Link
                        to={`/jobs/${job.id}`}
                        className="font-medium text-accent hover:underline"
                      >
                        View
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="mt-8 rounded-2xl border border-dashed border-line px-5 py-8 text-sm text-slate-600">
            No jobs yet.
          </p>
        )
      ) : null}
    </section>
  );
}
