import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getJob, getResultFileUrl, retryJob } from "../lib/api";
import { canRetry, formatDate } from "../lib/format";
import { StatusBadge } from "../components/StatusBadge";
import type { Job } from "../types";

export function JobDetailPage() {
  const { id = "" } = useParams();
  const [job, setJob] = useState<Job | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retrying, setRetrying] = useState(false);

  useEffect(() => {
    let mounted = true;
    let timer: number | undefined;

    async function load() {
      try {
        const result = await getJob(id);
        if (!mounted) {
          return;
        }

        setJob(result);
        setError(null);

        if (result.status === "Pending" || result.status === "Processing") {
          timer = window.setTimeout(() => {
            void load();
          }, 3000);
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
      if (timer) {
        window.clearTimeout(timer);
      }
    };
  }, [id]);

  async function handleRetry() {
    if (!job) {
      return;
    }

    setRetrying(true);
    setError(null);

    try {
      await retryJob(job.id);
      const refreshed = await getJob(job.id);
      setJob(refreshed);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "重试失败。");
    } finally {
      setRetrying(false);
    }
  }

  return (
    <section className="space-y-6">
      <div className="flex items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
            Job Detail
          </p>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight">任务详情</h1>
        </div>
        <Link
          to="/jobs"
          className="rounded-full border border-line px-4 py-2 text-sm font-medium text-ink transition hover:bg-soft"
        >
          返回列表
        </Link>
      </div>

      {loading ? <p className="text-sm text-slate-600">加载中...</p> : null}
      {error ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}

      {job ? (
        <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
          <div className="rounded-3xl border border-line bg-white p-8 shadow-card">
            <div className="flex items-center justify-between gap-4">
              <h2 className="text-2xl font-semibold">{job.name}</h2>
              <StatusBadge status={job.status} />
            </div>

            <dl className="mt-8 grid gap-5 sm:grid-cols-2">
              <div>
                <dt className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">任务类型</dt>
                <dd className="mt-2 text-sm">{job.type}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">创建时间</dt>
                <dd className="mt-2 text-sm">{formatDate(job.createdAtUtc)}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">开始时间</dt>
                <dd className="mt-2 text-sm">{formatDate(job.startedAtUtc)}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">完成时间</dt>
                <dd className="mt-2 text-sm">{formatDate(job.completedAtUtc)}</dd>
              </div>
            </dl>

            <div className="mt-8">
              <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-slate-500">
                转换结果
              </h3>
              <div className="mt-3 rounded-2xl border border-line bg-soft px-4 py-4 text-sm text-slate-700">
                {job.status === "Succeeded"
                  ? "转换完成，可以下载生成后的 PDF。"
                  : job.status === "Failed"
                    ? job.errorMessage || "任务失败。"
                    : "任务仍在处理中，请稍后自动刷新。"}
              </div>
            </div>
          </div>

          <aside className="rounded-3xl border border-line bg-white p-8 shadow-card">
            <h2 className="text-lg font-semibold">操作</h2>
            <div className="mt-6 space-y-4">
              <a
                href={job.status === "Succeeded" ? getResultFileUrl(job.id) : undefined}
                className={`flex w-full items-center justify-center rounded-full px-4 py-3 text-sm font-semibold ${
                  job.status === "Succeeded"
                    ? "bg-accent text-white hover:bg-[#214530]"
                    : "cursor-not-allowed bg-slate-200 text-slate-500"
                }`}
              >
                下载转换后的 PDF
              </a>

              <button
                type="button"
                onClick={() => void handleRetry()}
                disabled={!canRetry(job.status) || retrying}
                className="w-full rounded-full border border-line px-4 py-3 text-sm font-semibold text-ink transition hover:bg-soft disabled:cursor-not-allowed disabled:opacity-50"
              >
                {retrying ? "重试中..." : "失败后重试"}
              </button>
            </div>

            <div className="mt-8 rounded-2xl border border-line bg-soft px-4 py-4 text-sm leading-7 text-slate-600">
              <p>当前状态：{job.status}</p>
              <p>重试次数：{job.retryCount}</p>
              <p>原文件信息：由后端任务 payload 保存并异步处理。</p>
            </div>
          </aside>
        </div>
      ) : null}
    </section>
  );
}
