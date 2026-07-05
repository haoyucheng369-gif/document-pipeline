import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { StatusBadge } from "../components/StatusBadge";
import { useToast } from "../components/ToastProvider";
import { formatDate } from "../lib/format";
import { getJobs } from "../lib/api";
import { queryClient } from "../lib/queryClient";
import { subscribeToJobUpdates } from "../lib/signalr";

type JobsPageLocationState = {
  createdJobCount?: number;
};

// Job list page. TanStack Query owns server state; SignalR decides when to refresh it.
export function JobsPage() {
  const jobsQuery = useQuery({
    queryKey: ["jobs"],
    queryFn: getJobs
  });

  const { showToast } = useToast();
  const location = useLocation();
  const navigate = useNavigate();
  const locationState = location.state as JobsPageLocationState | null;
  const hasShownCreatedToastRef = useRef(false);
  const refreshTimerRef = useRef<number | null>(null);

  useEffect(() => {
    // Show the multi-create success toast once after navigation from the create page.
    if (!locationState?.createdJobCount) {
      hasShownCreatedToastRef.current = false;
      return;
    }

    if (hasShownCreatedToastRef.current) {
      return;
    }

    hasShownCreatedToastRef.current = true;

    showToast({
      type: "success",
      title: `${locationState.createdJobCount} conversion jobs submitted.`,
      description: "The jobs are now queued for background processing."
    });

    navigate(location.pathname, {
      replace: true,
      state: null
    });
  }, [location.pathname, locationState?.createdJobCount, navigate, showToast]);

  useEffect(() => {
    let unsubscribe: (() => void) | undefined;

    // Debounce realtime refreshes so quick status bursts do not trigger many GET requests.
    void subscribeToJobUpdates(async () => {
      if (refreshTimerRef.current !== null) {
        window.clearTimeout(refreshTimerRef.current);
      }

      refreshTimerRef.current = window.setTimeout(async () => {
        refreshTimerRef.current = null;
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
  }, []);

  return (
    <section className="rounded-3xl border border-line bg-white p-8 shadow-sm">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-accent">
            Monitoring
          </p>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight text-ink">
            Job List
          </h1>
        </div>

        <Link
          to="/"
          className="rounded-full border border-line px-5 py-3 text-sm font-medium text-ink transition hover:bg-soft"
        >
          New Task
        </Link>
      </div>

      {jobsQuery.isPending ? (
        <p className="mt-8 text-sm text-slate-600">Loading...</p>
      ) : null}

      {jobsQuery.error ? (
        <div className="mt-8 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {jobsQuery.error.message}
        </div>
      ) : null}

      {jobsQuery.data ? (
        jobsQuery.data.length > 0 ? (
          <div className="mt-8 overflow-hidden rounded-2xl border border-line">
            <table className="min-w-full divide-y divide-line text-sm">
              <thead className="bg-soft text-left text-slate-500">
                <tr>
                  <th className="px-5 py-4 font-medium">Task Name</th>
                  <th className="px-5 py-4 font-medium">Status</th>
                  <th className="px-5 py-4 font-medium">Created At</th>
                  <th className="px-5 py-4 font-medium">Retryable</th>
                  <th className="px-5 py-4 font-medium">Details</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line bg-white">
                {jobsQuery.data.map((job) => (
                  <tr key={job.id} className="align-middle">
                    <td className="px-5 py-4 font-medium text-ink">{job.name}</td>
                    <td className="px-5 py-4">
                      <StatusBadge status={job.status} />
                    </td>
                    <td className="px-5 py-4 text-slate-600">
                      {formatDate(job.createdAtUtc)}
                    </td>
                    <td className="px-5 py-4 text-slate-600">
                      {job.status === "Failed" ? "Yes" : "No"}
                    </td>
                    <td className="px-5 py-4">
                      <Link
                        to={`/jobs/${job.id}`}
                        className="font-medium text-accent hover:underline"
                      >
                        View
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="mt-8 rounded-2xl border border-dashed border-line px-5 py-8 text-sm text-slate-600">
            No jobs yet.
          </p>
        )
      ) : null}
    </section>
  );
}
