import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { useNavigate } from "react-router-dom";
import { z } from "zod";
import { createDocumentToPdf } from "../lib/api";

const createJobSchema = z.object({
  name: z.string().max(120, "Task name is too long.").optional(),
  file: z
    .any()
    .refine((value) => value instanceof FileList && value.length > 0, "Please choose a file.")
});

type CreateJobFormValues = z.infer<typeof createJobSchema>;

// 创建任务页：用 react-hook-form 管表单状态，用 mutation 负责提交异步任务。
export function CreateJobPage() {
  const navigate = useNavigate();
  const {
    formState: { errors },
    handleSubmit,
    register,
    watch
  } = useForm<CreateJobFormValues>({
    resolver: zodResolver(createJobSchema),
    defaultValues: {
      name: ""
    }
  });

  const selectedFile = watch("file")?.[0];

  // 提交 mutation：上传文件并创建任务，成功后跳到详情页继续观察状态。
  const createJobMutation = useMutation({
    mutationFn: async (values: CreateJobFormValues) => {
      const file = values.file[0] as File;
      return createDocumentToPdf(file, values.name);
    },
    onSuccess: (response) => {
      navigate(`/jobs/${response.jobId}`);
    }
  });

  return (
    <div className="grid gap-8 lg:grid-cols-[1.2fr_0.8fr]">
      <section className="rounded-3xl border border-line bg-white p-8 shadow-sm">
        <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
          Async Conversion
        </p>
        <h1 className="mt-3 text-4xl font-semibold tracking-tight text-ink">
          Upload a simple document and convert it to PDF asynchronously
        </h1>
        <p className="mt-4 max-w-2xl text-sm leading-7 text-slate-600">
          This demo currently supports images, plain text, markdown, and simple
          HTML files. The API creates a job, writes an outbox message, and lets
          the worker complete the conversion in the background.
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
            {errors.name ? (
              <p className="text-sm text-red-700">{errors.name.message}</p>
            ) : null}
          </div>

          <div className="space-y-3">
            <label className="text-sm font-medium text-slate-700">
              Choose File
            </label>
            <label className="flex cursor-pointer items-center justify-between rounded-2xl border border-dashed border-line bg-soft px-4 py-5 transition hover:border-accent/60">
              <span className="text-sm text-slate-600">
                {selectedFile
                  ? `Selected: ${selectedFile.name}`
                  : "Click to choose a supported file"}
              </span>
              <span className="rounded-full bg-white px-4 py-2 text-sm font-medium text-ink shadow-sm">
                Browse
              </span>
              <input
                {...register("file")}
                type="file"
                className="hidden"
                accept=".jpg,.jpeg,.png,.bmp,.gif,.webp,.txt,.md,.html,.htm,text/plain,text/markdown,text/html,image/*"
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
          <li>1. The client uploads a file and the API creates Job and Outbox.</li>
          <li>2. OutboxPublisherWorker scans and publishes a message to RabbitMQ.</li>
          <li>
            3. Job Worker receives the message and claims processing through Inbox
            and TryClaim.
          </li>
          <li>
            4. Worker converts the document to PDF and writes the result back to
            Job.
          </li>
          <li>
            5. The frontend polls job status and downloads the PDF after success.
          </li>
        </ol>
      </aside>
    </div>
  );
}
