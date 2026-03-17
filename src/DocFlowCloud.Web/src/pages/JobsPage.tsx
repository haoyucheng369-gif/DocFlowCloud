import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { StatusBadge } from "../components/StatusBadge";
import { formatDate } from "../lib/format";
import { getJobs } from "../lib/api";

// 任务列表页：用 Query 管理服务端列表数据，统一 loading / error / success 状态。
export function JobsPage() {
  const jobsQuery = useQuery({
    queryKey: ["jobs"],
    queryFn: getJobs
  });

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

      {jobsQuery.isPending ? (
        <p className="mt-8 text-sm text-slate-600">Loading...</p>
      ) : null}

      {jobsQuery.error ? (
        <div className="mt-8 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {jobsQuery.error.message}
        </div>
      ) : null}

      {jobsQuery.data ? (
        jobsQuery.data.length > 0 ? (
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
                {jobsQuery.data.map((job) => (
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
