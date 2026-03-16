import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getJobs } from "../lib/api";
import { canRetry, formatDate } from "../lib/format";
import { StatusBadge } from "../components/StatusBadge";
import type { Job } from "../types";

export function JobsPage() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;

    async function load() {
      try {
        const result = await getJobs();
        if (mounted) {
          setJobs(result);
          setError(null);
        }
      } catch (requestError) {
        if (mounted) {
          setError(requestError instanceof Error ? requestError.message : "加载任务失败。");
        }
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      mounted = false;
    };
  }, []);

  return (
    <section className="rounded-3xl border border-line bg-white p-8 shadow-card">
      <div className="flex items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
            Jobs
          </p>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight">任务列表</h1>
        </div>
        <Link
          to="/"
          className="rounded-full border border-line px-4 py-2 text-sm font-medium text-ink transition hover:bg-soft"
        >
          新建任务
        </Link>
      </div>

      {loading ? <p className="mt-8 text-sm text-slate-600">加载中...</p> : null}
      {error ? <p className="mt-8 text-sm text-red-600">{error}</p> : null}

      {!loading && !error ? (
        <div className="mt-8 overflow-hidden rounded-2xl border border-line">
          <table className="min-w-full divide-y divide-line text-sm">
            <thead className="bg-soft">
              <tr className="text-left text-slate-600">
                <th className="px-5 py-4 font-medium">任务名</th>
                <th className="px-5 py-4 font-medium">状态</th>
                <th className="px-5 py-4 font-medium">创建时间</th>
                <th className="px-5 py-4 font-medium">可重试</th>
                <th className="px-5 py-4 font-medium">详情</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line bg-white">
              {jobs.map((job) => (
                <tr key={job.id}>
                  <td className="px-5 py-4 font-medium">{job.name}</td>
                  <td className="px-5 py-4">
                    <StatusBadge status={job.status} />
                  </td>
                  <td className="px-5 py-4 text-slate-600">{formatDate(job.createdAtUtc)}</td>
                  <td className="px-5 py-4 text-slate-600">{canRetry(job.status) ? "是" : "否"}</td>
                  <td className="px-5 py-4">
                    <Link to={`/jobs/${job.id}`} className="font-medium text-accent hover:underline">
                      查看
                    </Link>
                  </td>
                </tr>
              ))}
              {jobs.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-5 py-8 text-center text-slate-500">
                    暂无任务。
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}
