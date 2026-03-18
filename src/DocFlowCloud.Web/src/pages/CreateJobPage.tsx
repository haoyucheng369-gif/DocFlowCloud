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

  return (
    <div className="grid gap-8 lg:grid-cols-[1.2fr_0.8fr]">
      <section className="rounded-3xl border border-line bg-white p-8 shadow-sm">
        <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
          Async Conversion
        </p>
        <h1 className="mt-3 text-4xl font-semibold tracking-tight text-ink">
          Upload one or more simple documents and convert them to PDF asynchronously
        </h1>
        <p className="mt-4 max-w-2xl text-sm leading-7 text-slate-600">
          This demo currently supports images, plain text, markdown, and simple
          HTML files. The API creates jobs, writes outbox messages, and lets the
          worker complete the conversion in the background.
        </p>

        <form
          className="mt-8 space-y-6"
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
              Optional for a single file. When multiple files are uploaded, each
              file becomes its own job.
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
              className={`flex cursor-pointer flex-col gap-3 rounded-2xl border border-dashed px-4 py-6 transition ${
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
                  Drag and drop files here, or click to choose multiple supported
                  files.
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
            className="inline-flex items-center justify-center rounded-full bg-accent px-6 py-3 text-sm font-semibold text-white transition hover:bg-accent/90 disabled:cursor-not-allowed disabled:bg-accent/60"
          >
            {createJobMutation.isPending ? "Submitting..." : "Submit Conversion Job"}
          </button>
        </form>
      </section>

      <aside className="rounded-3xl border border-line bg-white p-8 shadow-sm">
        <h2 className="text-lg font-semibold text-ink">Current Flow</h2>
        <ol className="mt-5 space-y-4 text-sm leading-7 text-slate-600">
          <li>1. The client uploads one or more files and the API creates jobs and outbox entries.</li>
          <li>2. OutboxPublisherWorker scans and publishes messages to RabbitMQ.</li>
          <li>
            3. Job Worker receives each message and claims processing through Inbox
            and TryClaim.
          </li>
          <li>
            4. Worker converts each document to PDF and writes the result back to
            storage.
          </li>
          <li>
            5. The frontend refreshes list/detail views when SignalR pushes job
            status changes.
          </li>
        </ol>
      </aside>
    </div>
  );
}
