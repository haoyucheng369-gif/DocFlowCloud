import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { createDocumentToPdf } from "../lib/api";

export function CreateJobPage() {
  const navigate = useNavigate();
  const [file, setFile] = useState<File | null>(null);
  const [name, setName] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!file) {
      setError("请选择一个文件。");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const result = await createDocumentToPdf(file, name);
      navigate(`/jobs/${result.jobId}`);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "提交任务失败。");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1.1fr_0.9fr]">
      <section className="rounded-3xl border border-line bg-white p-8 shadow-card">
        <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
          Document To PDF
        </p>
        <h1 className="mt-3 text-4xl font-semibold tracking-tight">
          上传简单文档，异步转换成 PDF
        </h1>
        <p className="mt-4 max-w-2xl text-base leading-7 text-slate-600">
          当前支持图片、txt、md、html。提交后会创建一个异步任务，由后端 Worker 继续处理。
        </p>

        <form className="mt-10 space-y-6" onSubmit={handleSubmit}>
          <div className="space-y-2">
            <label className="text-sm font-medium">任务名称</label>
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="例如：项目说明文档转换"
              className="w-full rounded-2xl border border-line px-4 py-3 outline-none transition focus:border-accent"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">选择文件</label>
            <input
              type="file"
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
              className="block w-full rounded-2xl border border-dashed border-line bg-soft px-4 py-6 text-sm file:mr-4 file:rounded-full file:border-0 file:bg-accent file:px-4 file:py-2 file:text-sm file:font-semibold file:text-white"
            />
            {file ? <p className="text-sm text-slate-600">已选择：{file.name}</p> : null}
          </div>

          {error ? (
            <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          ) : null}

          <button
            type="submit"
            disabled={submitting}
            className="inline-flex rounded-full bg-accent px-6 py-3 text-sm font-semibold text-white transition hover:bg-[#214530] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting ? "提交中..." : "提交转换任务"}
          </button>
        </form>
      </section>

      <section className="rounded-3xl border border-line bg-white p-8 shadow-card">
        <h2 className="text-lg font-semibold">当前流程</h2>
        <ol className="mt-6 space-y-4 text-sm leading-7 text-slate-600">
          <li>1. 前端上传文件，API 创建 Job 和 Outbox。</li>
          <li>2. OutboxPublisherWorker 扫描并发布消息到 RabbitMQ。</li>
          <li>3. Job Worker 收到消息后通过 Inbox 和 TryClaim 抢处理权。</li>
          <li>4. Worker 执行文档转 PDF，成功后更新 Job 状态并写回结果。</li>
          <li>5. 前端在任务详情页轮询状态，成功后直接下载 PDF。</li>
        </ol>
      </section>
    </div>
  );
}
