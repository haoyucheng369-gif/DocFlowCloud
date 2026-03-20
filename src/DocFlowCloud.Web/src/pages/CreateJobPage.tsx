import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { useNavigate } from "react-router-dom";
import { z } from "zod";
import { useToast } from "../components/ToastProvider";
import { createDocumentToPdf } from "../lib/api";

const createJobSchema = z.object({
  name: z.string().max(120, "Task name is too long.").optional(),
  files: z.array(z.instanceof(File)).min(1, "Please choose at least one file.")
});

type CreateJobFormValues = z.infer<typeof createJobSchema>;

// 创建任务页：
// 统一把拖拽和点击选择都收敛到 File[]，避免 FileList 在不同交互路径下表现不一致。
// 后端当前仍是单文件接口，所以前端会把多文件拆成多个请求，分别创建多个任务。
export function CreateJobPage() {
  const navigate = useNavigate();
  const { showToast } = useToast();
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const {
    formState: { errors },
    handleSubmit,
    register,
    setValue,
    watch
  } = useForm<CreateJobFormValues>({
    resolver: zodResolver(createJobSchema),
    defaultValues: {
      name: "",
      files: []
    }
  });

  const selectedFiles = watch("files") ?? [];

  // 多文件上传时逐个创建转换任务。
  // 单文件时保留输入的任务名；多文件时自动给每个任务补序号。
  const createJobMutation = useMutation({
    mutationFn: async (values: CreateJobFormValues) => {
      return await Promise.all(
        values.files.map((file, index) =>
          createDocumentToPdf(
            file,
            values.files.length === 1
              ? values.name
              : values.name?.trim()
                ? `${values.name.trim()} ${index + 1}`
                : undefined
          )
        )
      );
    },
    onSuccess: (responses) => {
      if (responses.length === 1) {
        showToast({
          type: "success",
          title: "Conversion job submitted.",
          description: `Job ID: ${responses[0].jobId}`
        });

        navigate(`/jobs/${responses[0].jobId}`);
        return;
      }

      navigate("/jobs", {
        state: {
          createdJobCount: responses.length
        }
      });
    },
    onError: (error) => {
      showToast({
        type: "error",
        title: "Failed to submit conversion job.",
        description: error.message
      });
    }
  });

  // 统一处理文件选择结果，确保点击选择和拖拽上传共用同一份表单状态。
  function applyFiles(fileList: FileList | null) {
    setValue("files", fileList ? Array.from(fileList) : [], {
      shouldDirty: true,
      shouldTouch: true,
      shouldValidate: true
    });
  }

  // 拖拽上传只负责接收文件，真正提交仍由表单统一触发。
  function handleDrop(files: FileList | null) {
    setIsDragging(false);
    applyFiles(files);
  }

  const selectedLabel =
    selectedFiles.length === 0
      ? "Click or drag files here"
      : selectedFiles.length === 1
        ? `Selected: ${selectedFiles[0].name}`
        : `${selectedFiles.length} files selected`;

  const stackGroups = [
    {
      title: "Frontend",
      items: ["React", "TypeScript", "Tailwind", "TanStack Query", "SignalR"]
    },
    {
      title: "Backend",
      items: ["ASP.NET Core", "RabbitMQ", "Outbox / Inbox", "Retry / DLQ", "Local / Blob Storage"]
    },
    {
      title: "Delivery",
      items: ["Docker Compose", "Testbed / Production", "Health Checks", "Unit + Integration Tests"]
    }
  ];

  return (
    <div className="grid gap-6 lg:grid-cols-[1.35fr_0.85fr]">
      <section className="space-y-6">
        <div className="rounded-3xl border border-line bg-white p-8 shadow-sm">
          <p className="text-xs font-semibold uppercase tracking-[0.24em] text-accent">
            Document To PDF System
          </p>
          <div className="mt-4 grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
            <div className="space-y-4">
              <h1 className="max-w-2xl text-3xl font-semibold tracking-tight text-ink">
                Asynchronous document conversion with queue-driven processing and realtime status updates
              </h1>
              <p className="max-w-2xl text-sm leading-7 text-slate-600">
                This homepage focuses on the system itself: upload a file, persist
                a job and outbox message, process conversion in the background, and
                push status updates back to the UI through SignalR.
              </p>
              <div className="flex flex-wrap gap-2">
                {["Images", "Plain Text", "Markdown", "Simple HTML", "PDF Output"].map((item) => (
                  <span
                    key={item}
                    className="inline-flex items-center rounded-full bg-soft px-3 py-1 text-xs font-medium text-slate-700 ring-1 ring-line"
                  >
                    {item}
                  </span>
                ))}
              </div>
            </div>

            <div className="rounded-2xl bg-soft p-5 ring-1 ring-line">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
                Quick Summary
              </p>
              <dl className="mt-4 space-y-3 text-sm">
                <div className="flex items-start justify-between gap-4">
                  <dt className="text-slate-500">Input</dt>
                  <dd className="text-right font-medium text-slate-800">
                    File upload with drag & drop or multi-select
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-4">
                  <dt className="text-slate-500">Execution</dt>
                  <dd className="text-right font-medium text-slate-800">
                    API + RabbitMQ + Worker
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-4">
                  <dt className="text-slate-500">Reliability</dt>
                  <dd className="text-right font-medium text-slate-800">
                    Outbox, Inbox, Retry, DLQ, Stale Recovery
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-4">
                  <dt className="text-slate-500">Storage</dt>
                  <dd className="text-right font-medium text-slate-800">
                    Local now, Azure Blob ready
                  </dd>
                </div>
              </dl>
            </div>
          </div>
        </div>

        <div className="grid gap-6 lg:grid-cols-[0.95fr_1.05fr]">
          <section className="rounded-3xl border border-line bg-white p-6 shadow-sm">
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-accent">
                  Tech Stack
                </p>
                <h2 className="mt-2 text-xl font-semibold text-ink">
                  Current building blocks
                </h2>
              </div>
            </div>

            <div className="mt-5 space-y-5">
              {stackGroups.map((group) => (
                <section key={group.title}>
                  <h3 className="text-sm font-semibold text-slate-700">{group.title}</h3>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {group.items.map((item) => (
                      <span
                        key={item}
                        className="inline-flex items-center rounded-full bg-soft px-3 py-1 text-xs font-medium text-slate-700 ring-1 ring-line"
                      >
                        {item}
                      </span>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          </section>

          <section className="rounded-3xl border border-line bg-white p-6 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-accent">
              Processing Flow
            </p>
            <h2 className="mt-2 text-xl font-semibold text-ink">
              From upload to realtime update
            </h2>

            <div className="mt-5 grid gap-3">
              {[
                ["1", "Client Upload", "The browser sends one or more files to the API."],
                ["2", "Job + Outbox", "The API stores the file, creates the job, and writes the outbox message."],
                ["3", "Queue Publish", "OutboxPublisherWorker sends the message to RabbitMQ."],
                ["4", "Worker Execute", "The worker claims the message, converts the file, and stores the PDF result."],
                ["5", "Status Push", "A status-changed event reaches the API, then SignalR notifies the frontend."]
              ].map(([step, title, description]) => (
                <div
                  key={step}
                  className="grid grid-cols-[2.25rem_1fr] gap-4 rounded-2xl bg-soft px-4 py-4 ring-1 ring-line"
                >
                  <div className="flex h-9 w-9 items-center justify-center rounded-full bg-accent text-sm font-semibold text-white">
                    {step}
                  </div>
                  <div>
                    <h3 className="text-sm font-semibold text-slate-800">{title}</h3>
                    <p className="mt-1 text-sm leading-6 text-slate-600">{description}</p>
                  </div>
                </div>
              ))}
            </div>
          </section>
        </div>
      </section>

      <aside className="rounded-3xl border border-line bg-white p-6 shadow-sm lg:sticky lg:top-8 lg:self-start">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-accent">
          Create Job
        </p>
        <h2 className="mt-2 text-xl font-semibold text-ink">
          Submit one or more files
        </h2>
        <p className="mt-3 text-sm leading-6 text-slate-600">
          The upload panel is intentionally smaller here. The homepage explains the
          system first, while the right side stays focused on creating tasks.
        </p>

        <form
          className="mt-6 space-y-5"
          onSubmit={handleSubmit((values) => createJobMutation.mutate(values))}
        >
          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-700">
              Task Name
            </label>
            <input
              {...register("name")}
              className="w-full rounded-2xl border border-line px-4 py-3 text-sm outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/15"
              placeholder="For example: Project overview document"
            />
            <p className="text-xs text-slate-500">
              Optional for a single file. Multiple files become multiple jobs.
            </p>
            {errors.name ? (
              <p className="text-sm text-red-700">{errors.name.message}</p>
            ) : null}
          </div>

          <div className="space-y-3">
            <label className="text-sm font-medium text-slate-700">
              Choose Files
            </label>

            <div
              className={`flex cursor-pointer flex-col gap-3 rounded-2xl border border-dashed px-4 py-5 transition ${
                isDragging
                  ? "border-accent bg-accent/5"
                  : "border-line bg-soft hover:border-accent/60"
              }`}
              role="button"
              tabIndex={0}
              onDragEnter={(event) => {
                event.preventDefault();
                setIsDragging(true);
              }}
              onDragOver={(event) => {
                event.preventDefault();
                setIsDragging(true);
              }}
              onDragLeave={(event) => {
                event.preventDefault();
                setIsDragging(false);
              }}
              onDrop={(event) => {
                event.preventDefault();
                handleDrop(event.dataTransfer.files);
              }}
              onClick={() => fileInputRef.current?.click()}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  fileInputRef.current?.click();
                }
              }}
            >
              <div className="flex items-center justify-between gap-4">
                <span className="text-sm text-slate-600">{selectedLabel}</span>
                <button
                  type="button"
                  className="rounded-full bg-white px-4 py-2 text-sm font-medium text-ink shadow-sm"
                  onClick={(event) => {
                    event.stopPropagation();
                    fileInputRef.current?.click();
                  }}
                >
                  Browse
                </button>
              </div>

              {selectedFiles.length > 0 ? (
                <ul className="space-y-1 text-sm text-slate-600">
                  {selectedFiles.slice(0, 5).map((file) => (
                    <li key={`${file.name}-${file.lastModified}`}>- {file.name}</li>
                  ))}
                  {selectedFiles.length > 5 ? (
                    <li>- ...and {selectedFiles.length - 5} more</li>
                  ) : null}
                </ul>
              ) : (
                <p className="text-xs text-slate-500">
                  Drag files here or click to choose multiple supported files.
                </p>
              )}

              <input
                ref={fileInputRef}
                type="file"
                className="hidden"
                multiple
                accept=".jpg,.jpeg,.png,.bmp,.gif,.webp,.txt,.md,.html,.htm,text/plain,text/markdown,text/html,image/*"
                onChange={(event) => {
                  applyFiles(event.target.files);
                }}
              />
            </div>

            {errors.files ? (
              <p className="text-sm text-red-700">
                {errors.files.message?.toString()}
              </p>
            ) : null}
          </div>

          {createJobMutation.error ? (
            <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {createJobMutation.error.message}
            </div>
          ) : null}

          <button
            type="submit"
            disabled={createJobMutation.isPending}
            className="inline-flex w-full items-center justify-center rounded-full bg-accent px-6 py-3 text-sm font-semibold text-white transition hover:bg-accent/90 disabled:cursor-not-allowed disabled:bg-accent/60"
          >
            {createJobMutation.isPending ? "Submitting..." : "Submit Conversion Job"}
          </button>
        </form>
      </aside>
    </div>
  );
}
