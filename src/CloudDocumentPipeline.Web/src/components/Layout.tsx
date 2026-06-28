import { Link, NavLink } from "react-router-dom";
import type { PropsWithChildren } from "react";
import { useQuery } from "@tanstack/react-query";
import { getSystemEnvironment, API_BASE_URL } from "../lib/api";
import { getRuntimeAppEnvironment } from "../lib/runtimeConfig";

const navClassName = ({ isActive }: { isActive: boolean }) =>
  [
    "rounded-full px-4 py-2 text-sm font-medium transition-colors",
    isActive ? "bg-accent text-white" : "text-ink hover:bg-soft"
  ].join(" ");

function getEnvironmentBadgeClass(environment?: string) {
  switch ((environment ?? "").toLowerCase()) {
    case "production":
      return "bg-rose-100 text-rose-800 ring-1 ring-rose-200";
    case "testbed":
    case "staging":
      return "bg-amber-100 text-amber-800 ring-1 ring-amber-200";
    case "development":
    case "dev":
      return "bg-slate-100 text-slate-700 ring-1 ring-slate-200";
    default:
      return "bg-sky-100 text-sky-800 ring-1 ring-sky-200";
  }
}

function EnvironmentChip({
  label,
  value,
  fullValue,
  emphasize = false
}: {
  label: string;
  value: string;
  fullValue?: string;
  emphasize?: boolean;
}) {
  return (
    <span className="group relative inline-flex">
      <span className="inline-flex items-center gap-2 rounded-full bg-white px-2.5 py-1 align-middle ring-1 ring-slate-200">
        <span className="inline-flex items-center text-[11px] font-semibold uppercase tracking-[0.12em] leading-none text-slate-500">
          {label}
        </span>
        <span
          className={[
            "inline-flex max-w-[13rem] items-center truncate rounded-full px-2.5 py-1 text-[11px] font-semibold leading-none",
            emphasize ? getEnvironmentBadgeClass(value) : "bg-slate-50 text-slate-700 ring-1 ring-slate-200"
          ].join(" ")}
        >
          {value}
        </span>
      </span>
      {(fullValue ?? value) ? (
        <span className="pointer-events-none absolute left-1/2 top-full z-20 mt-2 hidden w-max max-w-[22rem] -translate-x-1/2 rounded-2xl bg-slate-900 px-3 py-2 text-[11px] font-medium leading-5 text-white shadow-lg group-hover:block">
          {fullValue ?? value}
        </span>
      ) : null}
    </span>
  );
}

export function Layout({ children }: PropsWithChildren) {
  const {
    data: environment,
    isLoading: isEnvironmentLoading,
    isError: isEnvironmentError
  } = useQuery({
    queryKey: ["system-environment"],
    queryFn: getSystemEnvironment,
    staleTime: 60_000
  });

  const frontendEnvironment = getRuntimeAppEnvironment();
  const environmentFallback = isEnvironmentLoading ? "Loading..." : "Unavailable";
  const databaseValue = environment
    ? environment.databaseName
    : environmentFallback;
  const databaseFullValue = environment
    ? `${environment.databaseServer} / ${environment.databaseName}`
    : environmentFallback;
  const messagingValue = environment
    ? environment.messagingProvider
    : environmentFallback;
  const messagingFullValue = environment
    ? `${environment.messagingProvider} (${environment.messagingTarget})`
    : environmentFallback;
  const apiBaseUrlValue = (() => {
    try {
      return new URL(API_BASE_URL).host;
    } catch {
      return API_BASE_URL;
    }
  })();

  return (
    <div className="min-h-screen bg-mist text-ink">
      <header className="border-b border-line bg-white/90 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-5">
          <Link to="/" className="text-xl font-semibold tracking-tight">
            CloudDocumentPipeline
          </Link>
          <nav className="flex gap-2 rounded-full border border-line bg-white p-1">
            <NavLink to="/" end className={navClassName}>
              New Task
            </NavLink>
            <NavLink to="/jobs" className={navClassName}>
              Jobs
            </NavLink>
          </nav>
        </div>
        <div className="border-t border-line bg-soft/60">
          <div className="mx-auto flex max-w-6xl flex-wrap gap-x-4 gap-y-2 px-6 py-3 text-xs text-slate-600">
            <EnvironmentChip label="Frontend" value={frontendEnvironment} emphasize />
            <EnvironmentChip
              label="API"
              value={environment?.apiEnvironment ?? environmentFallback}
              emphasize
            />
            <EnvironmentChip
              label="Database"
              value={databaseValue}
              fullValue={databaseFullValue}
            />
            <EnvironmentChip
              label="Messaging"
              value={messagingValue}
              fullValue={messagingFullValue}
            />
            <EnvironmentChip
              label="Storage"
              value={environment?.storageProvider ?? environmentFallback}
            />
            <EnvironmentChip
              label="API Base URL"
              value={apiBaseUrlValue}
              fullValue={API_BASE_URL}
            />
            {isEnvironmentError ? (
              <span className="inline-flex items-center rounded-full bg-rose-50 px-2.5 py-1 text-[11px] font-medium text-rose-700 ring-1 ring-rose-200">
                Environment endpoint unavailable on current API instance.
              </span>
            ) : null}
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 py-10">{children}</main>
    </div>
  );
}
