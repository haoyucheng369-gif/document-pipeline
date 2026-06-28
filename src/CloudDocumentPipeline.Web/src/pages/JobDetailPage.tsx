import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { Link, useParams } from "react-router-dom";
import { StatusBadge } from "../components/StatusBadge";
import { useToast } from "../components/ToastProvider";
import { formatDate } from "../lib/format";
import { downloadResultFile, getJob, retryJob } from "../lib/api";
import { subscribeToJobUpdates } from "../lib/signalr";

// 任务详情页：
// 详情数据仍然由 TanStack Query 管理，但刷新时机由 SignalR 驱动。
// 当前页只关心当前 job 的状态变化，收到对应 jobUpdated 后再刷新详情和列表缓存。
export function JobDetailPage() {
  const { id = "" } = useParams();
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const refreshTimerRef = useRef<number | null>(null);

  const jobQuery = useQuery({
    queryKey: ["job", id],
    queryFn: () => getJob(id)
  });

  // 当前这个 job 状态变化时，刷新详情和列表缓存。
  // 同样加一个轻量防抖，避免短时间连续事件导致重复 GET。
  useEffect(() => {
    let unsubscribe: (() => void) | undefined;

    void subscribeToJobUpdates(async (payload) => {
      if (payload.jobId !== id) {
        return;
      }

      if (refreshTimerRef.current !== null) {
        window.clearTimeout(refreshTimerRef.current);
      }

      refreshTimerRef.current = window.setTimeout(async () => {
        refreshTimerRef.current = null;
        await queryClient.invalidateQueries({ queryKey: ["job", id] });
        await queryClient.invalidateQueries({ queryKey: ["jobs"] });
      }, 250);
    }).then((cleanup) => {
      unsubscribe = cleanup;
    });

    return () => {
      if (refreshTimerRef.current !== null) {
        window.clearTimeout(refreshTimerRef.current);
        refreshTimerRef.current = null;
      }

      unsubscribe?.();
    };
  }, [id, queryClient]);

  const retryMutation = useMutation({
    mutationFn: () => retryJob(id),
    onSuccess: async () => {
      showToast({
        type: "success",
        title: "Retry started.",
        description: "The failed job has been queued again."
      });
      await queryClient.invalidateQueries({ queryKey: ["job", id] });
      await queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
    onError: (error) => {
      showToast({
        type: "error",
        title: "Failed to retry job.",
        description: error.message
      });
    }
  });

  const downloadMutation = useMutation({
    mutationFn: () => downloadResultFile(id),
    onSuccess: ({ blob, fileName }) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);

      showToast({
        type: "success",
        title: "Download started.",
        description: fileName
      });
    },
    onError: (error) => {
      showToast({
        type: "error",
        title: "Failed to download file.",
        description: error.message
      });
    }
  });

  if (jobQuery.isPending) {
    return <p className="text-sm text-slate-600">Loading...</p>;
  }

  if (!jobQuery.data) {
    return (
      <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        {jobQuery.error?.message ?? "The requested job was not found."}
      </div>
    );
  }

  const job = jobQuery.data;

  return (
    <div className="grid gap-8 lg:grid-cols-[1.2fr_0.8fr]">
      <section className="rounded-3xl border border-line bg-white p-8 shadow-sm">
        <div className="flex items-start justify-between gap-6">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
              Job Tracking
            </p>
            <h1 className="mt-2 text-3xl font-semibold tracking-tight text-ink">
              Job Detail
            </h1>
            <p className="mt-3 text-sm leading-7 text-slate-600">{job.name}</p>
          </div>

          <Link
            to="/jobs"
            className="rounded-full border border-line px-4 py-2 text-sm font-medium text-ink transition hover:bg-soft"
          >
            Back to Jobs
          </Link>
        </div>

        {jobQuery.error ? (
          <div className="mt-6 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {jobQuery.error.message}
          </div>
        ) : null}

        {retryMutation.error ? (
          <div className="mt-6 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {retryMutation.error.message}
          </div>
        ) : null}

        {downloadMutation.error ? (
          <div className="mt-6 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {downloadMutation.error.message}
          </div>
        ) : null}

        <div className="mt-8 grid gap-5 rounded-3xl bg-soft p-6 md:grid-cols-2">
          <InfoRow label="Task Type" value={job.type} />
          <InfoRow label="Created At" value={formatDate(job.createdAtUtc)} />
          <InfoRow
            label="Started At"
            value={job.startedAtUtc ? formatDate(job.startedAtUtc) : "Not started"}
          />
          <InfoRow
            label="Completed At"
            value={
              job.completedAtUtc ? formatDate(job.completedAtUtc) : "Not completed"
            }
          />
        </div>

        <div className="mt-8 rounded-3xl border border-line p-6">
          <div className="flex items-center justify-between gap-4">
            <h2 className="text-lg font-semibold text-ink">Conversion Result</h2>
            <StatusBadge status={job.status} />
          </div>

          <div className="mt-4 space-y-3 text-sm leading-7 text-slate-600">
            {job.status === "Succeeded" ? (
              <p>
                The conversion is complete. You can download the generated PDF.
              </p>
            ) : null}

            {job.status === "Failed" ? (
              <p>{job.errorMessage || "The job failed."}</p>
            ) : null}

            {job.status === "Pending" || job.status === "Processing" ? (
              <p>
                The job is still running. This page refreshes automatically when the
                backend pushes a status update.
              </p>
            ) : null}

            <p>
              The backend stores input and output files through a storage provider
              and keeps only storage keys inside the job payload/result.
            </p>
          </div>
        </div>
      </section>

      <aside className="rounded-3xl border border-line bg-white p-8 shadow-sm">
        <h2 className="text-lg font-semibold text-ink">Actions</h2>

        <div className="mt-5 space-y-4 text-sm text-slate-600">
          <p>
            Current Status: <span className="font-medium text-ink">{job.status}</span>
          </p>
          <p>
            Retry Count:{" "}
            <span className="font-medium text-ink">{job.retryCount}</span>
          </p>
          <p>
            Correlation ID:{" "}
            <span className="break-all font-mono text-xs text-slate-500">
              {job.correlationId || "Unavailable"}
            </span>
          </p>
        </div>

        <div className="mt-8 space-y-3">
          {job.status === "Succeeded" ? (
            <button
              type="button"
              onClick={() => downloadMutation.mutate()}
              disabled={downloadMutation.isPending}
              className="inline-flex w-full items-center justify-center rounded-full bg-accent px-5 py-3 text-sm font-semibold text-white transition hover:bg-accent/90 disabled:cursor-not-allowed disabled:bg-accent/60"
            >
              {downloadMutation.isPending
                ? "Preparing download..."
                : "Download Converted PDF"}
            </button>
          ) : null}

          {job.status === "Failed" ? (
            <button
              type="button"
              disabled={retryMutation.isPending}
              onClick={() => retryMutation.mutate()}
              className="inline-flex w-full items-center justify-center rounded-full border border-line px-5 py-3 text-sm font-semibold text-ink transition hover:bg-soft disabled:cursor-not-allowed disabled:text-slate-400"
            >
              {retryMutation.isPending ? "Retrying..." : "Retry Failed Job"}
            </button>
          ) : null}
        </div>
      </aside>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">
        {label}
      </dt>
      <dd className="mt-2 text-sm font-medium text-ink">{value}</dd>
    </div>
  );
}
