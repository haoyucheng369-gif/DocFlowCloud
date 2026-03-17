import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useNavigate } from "react-router-dom";
import { z } from "zod";
import { useToast } from "../components/ToastProvider";
import { createDocumentToPdf } from "../lib/api";

const createJobSchema = z.object({
  name: z.string().max(120, "Task name is too long.").optional(),
  file: z
    .any()
    .refine(
      (value) => value instanceof FileList && value.length > 0,
      "Please choose at least one file."
    )
});

type CreateJobFormValues = z.infer<typeof createJobSchema>;

// 创建任务页：
// 支持普通选择、多文件选择和拖拽上传。后端当前仍是单文件接口，
// 所以前端会把多文件拆成多次请求，分别创建多个异步转换任务。
export function CreateJobPage() {
  const navigate = useNavigate();
  const { showToast } = useToast();
  const [isDragging, setIsDragging] = useState(false);

  const {
    formState: { errors },
    handleSubmit,
    register,
    setValue,
    watch
  } = useForm<CreateJobFormValues>({
    resolver: zodResolver(createJobSchema),
    defaultValues: {
      name: ""
    }
  });

  const fileRegistration = register("file");
  const watchedFiles = watch("file") as FileList | undefined;
  const selectedFiles = watchedFiles ? Array.from(watchedFiles) : [];

  // 提交 mutation：
  // 单文件时保留自定义任务名；
  // 多文件时逐个创建任务，并让每个任务默认使用各自文件名，避免一个名字对应多条任务。
  const createJobMutation = useMutation({
    mutationFn: async (values: CreateJobFormValues) => {
      const files = Array.from(values.file as FileList);

      const responses = await Promise.all(
        files.map((file, index) =>
          createDocumentToPdf(
            file,
            files.length === 1 ? values.name : values.name?.trim() ? `${values.name.trim()} ${index + 1}` : undefined
          )
        )
      );

      return responses;
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

  // 统一处理文件选择和拖拽后的 FileList 回填，
  // 这样表单校验和页面显示都走同一套状态。
  function applyFiles(fileList: FileList | null) {
    setValue("file", fileList, {
      shouldDirty: true,
      shouldTouch: true,
      shouldValidate: true
    });
  }

  // 拖拽区域只负责接收文件，不直接提交；
  // 真正提交仍然走表单，保持错误提示和 mutation 状态一致。
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
              Optional for a single file. When multiple files are uploaded, each file becomes its own job.
            </p>
            {errors.name ? (
              <p className="text-sm text-red-700">{errors.name.message}</p>
            ) : null}
          </div>

          <div className="space-y-3">
            <label className="text-sm font-medium text-slate-700">
              Choose Files
            </label>
            <label
              className={`flex cursor-pointer flex-col gap-3 rounded-2xl border border-dashed px-4 py-6 transition ${
                isDragging
                  ? "border-accent bg-accent/5"
                  : "border-line bg-soft hover:border-accent/60"
              }`}
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
            >
              <div className="flex items-center justify-between gap-4">
                <span className="text-sm text-slate-600">{selectedLabel}</span>
                <span className="rounded-full bg-white px-4 py-2 text-sm font-medium text-ink shadow-sm">
                  Browse
                </span>
              </div>

              {selectedFiles.length > 0 ? (
                <ul className="space-y-1 text-sm text-slate-600">
                  {selectedFiles.slice(0, 5).map((file) => (
                    <li key={`${file.name}-${file.lastModified}`}>• {file.name}</li>
                  ))}
                  {selectedFiles.length > 5 ? (
                    <li>• ...and {selectedFiles.length - 5} more</li>
                  ) : null}
                </ul>
              ) : (
                <p className="text-xs text-slate-500">
                  Drag and drop files here, or click to choose multiple supported files.
                </p>
              )}

              <input
                {...fileRegistration}
                type="file"
                className="hidden"
                multiple
                accept=".jpg,.jpeg,.png,.bmp,.gif,.webp,.txt,.md,.html,.htm,text/plain,text/markdown,text/html,image/*"
                onChange={(event) => {
                  fileRegistration.onChange(event);
                  applyFiles(event.target.files);
                }}
              />
            </label>
            {errors.file ? (
              <p className="text-sm text-red-700">{errors.file.message?.toString()}</p>
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
            5. The frontend polls job status and downloads each PDF after success.
          </li>
        </ol>
      </aside>
    </div>
  );
}
