import type { CreateJobResponse, Job, SystemEnvironment } from "../types";
import { getRuntimeApiBaseUrl } from "./runtimeConfig";

// Centralize frontend API calls so UI code only depends on business actions.
const API_BASE_URL = getRuntimeApiBaseUrl();

type ProblemDetails = {
  title?: string;
  detail?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
};

function toApiErrorMessage(problem: ProblemDetails) {
  const fieldErrors = problem.errors
    ? Object.values(problem.errors)
        .flat()
        .filter(Boolean)
    : [];

  const parts = [problem.title, problem.detail, ...fieldErrors].filter(Boolean);
  const message = parts.join(" ");

  if (message && problem.correlationId) {
    return `${message} (Correlation ID: ${problem.correlationId})`;
  }

  if (message) {
    return message;
  }

  if (problem.correlationId) {
    return `Request failed. Correlation ID: ${problem.correlationId}`;
  }

  return "Request failed.";
}

async function parseJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const contentType = response.headers.get("content-type") ?? "";

    if (contentType.includes("application/json")) {
      const problem = (await response.json()) as ProblemDetails;
      throw new Error(toApiErrorMessage(problem));
    }

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
  const response = await fetch(`${API_BASE_URL}/api/jobs`, {
    cache: "no-store"
  });
  return parseJson<Job[]>(response);
}

export async function getJob(id: string) {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}`, {
    cache: "no-store"
  });
  return parseJson<Job>(response);
}

export async function getSystemEnvironment() {
  const response = await fetch(`${API_BASE_URL}/api/system/environment`, {
    cache: "no-store"
  });

  return parseJson<SystemEnvironment>(response);
}

export async function retryJob(id: string) {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}/retry`, {
    method: "POST"
  });

  if (!response.ok) {
    const contentType = response.headers.get("content-type") ?? "";

    if (contentType.includes("application/json")) {
      const problem = (await response.json()) as ProblemDetails;
      throw new Error(toApiErrorMessage(problem));
    }

    const text = await response.text();
    throw new Error(text || `Retry failed with ${response.status}`);
  }
}

export async function downloadResultFile(id: string) {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}/result-file`, {
    cache: "no-store"
  });

  if (!response.ok) {
    const contentType = response.headers.get("content-type") ?? "";

    if (contentType.includes("application/json")) {
      const problem = (await response.json()) as ProblemDetails;
      throw new Error(toApiErrorMessage(problem));
    }

    const text = await response.text();
    throw new Error(text || `Download failed with ${response.status}`);
  }

  const disposition = response.headers.get("content-disposition") ?? "";
  const fileNameMatch = disposition.match(/filename=\"?([^\"]+)\"?/i);
  const fileName = fileNameMatch?.[1] ?? `job-${id}.pdf`;
  const blob = await response.blob();

  return {
    blob,
    fileName
  };
}

export { API_BASE_URL };
