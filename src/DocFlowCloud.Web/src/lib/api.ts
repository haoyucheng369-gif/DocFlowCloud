import type { CreateJobResponse, Job } from "../types";

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL?.toString() ?? "http://localhost:5000";

async function parseJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed with ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function createDocumentToPdf(file: File, name?: string) {
  const form = new FormData();
  form.append("file", file);

  if (name?.trim()) {
    form.append("name", name.trim());
  }

  const response = await fetch(`${API_BASE_URL}/api/jobs/document-to-pdf`, {
    method: "POST",
    body: form
  });

  return parseJson<CreateJobResponse>(response);
}

export async function getJobs() {
  const response = await fetch(`${API_BASE_URL}/api/jobs`);
  return parseJson<Job[]>(response);
}

export async function getJob(id: string) {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}`);
  return parseJson<Job>(response);
}

export async function retryJob(id: string) {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}/retry`, {
    method: "POST"
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Retry failed with ${response.status}`);
  }
}

export function getResultFileUrl(id: string) {
  return `${API_BASE_URL}/api/jobs/${id}/result-file`;
}
