import type { ReactNode } from "react";

export type InfoTab = "overview" | "architecture" | "flow" | "delivery" | "cloud";

const tabItems: Array<{ key: InfoTab; label: string }> = [
  { key: "overview", label: "Document To PDF System" },
  { key: "architecture", label: "Architecture" },
  { key: "flow", label: "Processing Flow" },
  { key: "delivery", label: "CI/CD" },
  { key: "cloud", label: "Cloud" }
];

const stackGroups = [
  {
    title: "Frontend",
    items: ["React", "TypeScript", "Tailwind", "TanStack Query", "SignalR"]
  },
  {
    title: "Architecture",
    items: ["ASP.NET Core", "Clean Architecture", "Outbox / Inbox", "Retry / DLQ", "Background Workers"]
  },
  {
    title: "Cloud and Delivery",
    items: ["Azure Container Apps", "Azure SQL", "Azure Blob", "Azure Service Bus", "Key Vault", "GitHub Actions", "GHCR", "Terraform"]
  },
  {
    title: "Observability",
    items: ["Serilog", "Health Checks", "Metrics", "OpenTelemetry", "Realtime Updates"]
  }
];

const cloudHighlights = [
  {
    title: "Environment Strategy",
    description:
      "Development keeps local storage and RabbitMQ for fast debugging, while testbed and production switch to Azure Blob, Azure Service Bus, and cloud-hosted apps."
  },
  {
    title: "Azure Services",
    description:
      "Azure Container Apps host web, API, worker, notification, and a migrator job. Azure SQL, Blob, Service Bus, and Key Vault provide the managed runtime building blocks."
  },
  {
    title: "Identity and Secrets",
    description:
      "Runtime secrets come from Azure Key Vault through Container Apps secret references and managed identities instead of hard-coded appsettings or GitHub secret passthrough."
  },
  {
    title: "Terraform Status",
    description:
      "Testbed has already been imported and aligned to zero drift in Terraform, while production is modeled as a clean create-from-scratch environment."
  }
];

const deliveryHighlights = [
  {
    title: "CI on test",
    description:
      "Pushes to test run CI, build and publish images to GHCR, run migrations when needed, and deploy the validated stack to the Azure testbed automatically."
  },
  {
    title: "Promotion to production",
    description:
      "Production does not rebuild. It promotes an image tag that already passed testbed validation, which keeps testbed and production aligned."
  },
  {
    title: "App and infra split",
    description:
      "Application delivery and Terraform validation now run in separate GitHub workflows, so infra changes no longer rebuild the app stack and app changes no longer trigger Terraform checks."
  },
  {
    title: "Rollback",
    description:
      "Rollback is the same promote action with an older known-good image tag, which keeps the release model simple and auditable."
  }
];

const releaseFlow = [
  ["1", "Upload", "Browser sends files to the API."],
  ["2", "Persist", "API stores file, job, and outbox record."],
  ["3", "Publish", "OutboxPublisherWorker forwards the event to Azure Service Bus."],
  ["4", "Process", "Worker converts the document and saves the PDF result."],
  ["5", "Notify", "API pushes the updated status back to the UI through SignalR."]
] as const;

const deliveryFlow = [
  ["1", "CI", "Pushes to test run build, tests, and image publication to GHCR."],
  ["2", "Testbed", "Validated images are deployed to Azure testbed automatically."],
  ["3", "Promote", "Production reuses a validated image tag instead of rebuilding from source."],
  ["4", "Infra", "Terraform manages environment shape while a separate workflow validates infra changes."]
] as const;

function Panel({
  eyebrow,
  title,
  children
}: {
  eyebrow: string;
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="min-w-0 overflow-hidden rounded-3xl border border-line bg-white p-5 shadow-sm sm:p-6">
      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
        {eyebrow}
      </p>
      <h2 className="mt-2 text-xl font-semibold text-ink">{title}</h2>
      <div className="mt-5">{children}</div>
    </section>
  );
}

function OverviewPanel() {
  return (
    <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
      <div className="min-w-0">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
          Document To PDF System
        </p>
        <h2 className="mt-2 max-w-lg break-words text-[1.55rem] font-semibold leading-tight text-ink sm:text-xl">
          Asynchronous document conversion with realtime updates
        </h2>
        <p className="mt-5 max-w-2xl text-sm leading-7 text-slate-600">
          This demo shows the whole path: upload a file, persist a job and
          outbox message, process conversion in the background, and push
          status updates back to the UI through SignalR.
        </p>
        <div className="mt-5 flex flex-wrap gap-2">
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

      <div className="min-w-0 rounded-2xl bg-soft p-5 ring-1 ring-line">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
          Quick Summary
        </p>
        <dl className="mt-4 space-y-3 text-sm">
          <div className="flex items-start justify-between gap-4">
            <dt className="text-slate-500">Input</dt>
            <dd className="max-w-[10rem] text-right font-medium text-slate-800 sm:max-w-[11rem]">
              Drag, drop, or multi-select
            </dd>
          </div>
          <div className="flex items-start justify-between gap-4">
            <dt className="text-slate-500">Execution</dt>
            <dd className="max-w-[10rem] text-right font-medium text-slate-800 sm:max-w-[11rem]">
              API, worker, realtime updates
            </dd>
          </div>
          <div className="flex items-start justify-between gap-4">
            <dt className="text-slate-500">Reliability</dt>
            <dd className="max-w-[10rem] text-right font-medium text-slate-800 sm:max-w-[11rem]">
              Outbox, inbox, retry, DLQ
            </dd>
          </div>
          <div className="flex items-start justify-between gap-4">
            <dt className="text-slate-500">Cloud</dt>
            <dd className="max-w-[10rem] text-right font-medium text-slate-800 sm:max-w-[11rem]">
              ACA, SQL, Blob, Service Bus, Key Vault
            </dd>
          </div>
        </dl>
      </div>
    </div>
  );
}

function ArchitecturePanel() {
  return (
    <div className="grid gap-6 lg:grid-cols-[1.05fr_0.95fr]">
      <Panel eyebrow="Architecture" title="Application structure">
        <div className="space-y-5">
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
      </Panel>

      <Panel eyebrow="Key Ideas" title="Design choices">
        <div className="grid gap-3">
          {[
            "Clean Architecture keeps application rules separate from infrastructure providers and hosting details.",
            "Outbox and inbox patterns keep async processing reliable across retries, restarts, and delayed consumers.",
            "SignalR closes the loop by pushing job state changes back to the browser instead of forcing manual refresh."
          ].map((line) => (
            <div
              key={line}
              className="rounded-2xl bg-soft px-4 py-4 text-sm leading-6 text-slate-700 ring-1 ring-line"
            >
              {line}
            </div>
          ))}
        </div>
      </Panel>
    </div>
  );
}

function FlowPanel() {
  return (
    <Panel eyebrow="Processing Flow" title="From upload to realtime update">
      <div className="grid gap-3">
        {releaseFlow.map(([step, title, description]) => (
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
    </Panel>
  );
}

function DeliveryPanel() {
  return (
    <Panel eyebrow="CI/CD" title="Delivery and promotion model">
      <div className="grid gap-6 lg:grid-cols-[0.95fr_1.05fr]">
        <div className="grid gap-3">
          {deliveryHighlights.map((item) => (
            <div
              key={item.title}
              className="rounded-2xl bg-soft px-4 py-4 ring-1 ring-line"
            >
              <h3 className="text-sm font-semibold text-slate-800">{item.title}</h3>
              <p className="mt-2 text-sm leading-6 text-slate-600">{item.description}</p>
            </div>
          ))}
        </div>

        <div className="grid gap-3">
          {deliveryFlow.map(([step, title, description]) => (
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
      </div>
    </Panel>
  );
}

function CloudPanel() {
  return (
    <Panel eyebrow="Cloud" title="Azure runtime model">
      <div className="grid gap-3">
        {cloudHighlights.map((item) => (
          <div
            key={item.title}
            className="min-w-0 rounded-2xl bg-soft px-4 py-4 ring-1 ring-line"
          >
            <h3 className="text-sm font-semibold text-slate-800">{item.title}</h3>
            <p className="mt-2 break-words text-sm leading-6 text-slate-600">{item.description}</p>
          </div>
        ))}
      </div>
    </Panel>
  );
}

export function InfoTabsPanel({
  activeTab,
  onTabChange
}: {
  activeTab: InfoTab;
  onTabChange: (tab: InfoTab) => void;
}) {
  return (
    <section className="min-w-0 rounded-3xl border border-line bg-white p-5 shadow-sm sm:p-6 lg:p-8">
      <div className="grid grid-cols-2 gap-1 rounded-2xl border border-line bg-soft/80 p-1.5 sm:grid-cols-3 lg:grid-cols-5">
          {tabItems.map((tab) => (
            <button
              key={tab.key}
              type="button"
              onClick={() => onTabChange(tab.key)}
              className={[
                "inline-flex min-w-0 items-center justify-center rounded-xl px-3 py-2.5 text-center text-sm font-semibold leading-snug transition sm:px-4",
                activeTab === tab.key
                  ? "bg-white text-accent shadow-sm ring-1 ring-accent/15"
                  : "text-slate-600 hover:bg-white/70 hover:text-slate-900"
              ].join(" ")}
              style={undefined}
              aria-pressed={activeTab === tab.key}
            >
              {tab.label}
            </button>
          ))}
      </div>

      <div className="mt-6">
        {activeTab === "overview" ? <OverviewPanel /> : null}
        {activeTab === "architecture" ? <ArchitecturePanel /> : null}
        {activeTab === "flow" ? <FlowPanel /> : null}
        {activeTab === "delivery" ? <DeliveryPanel /> : null}
        {activeTab === "cloud" ? <CloudPanel /> : null}
      </div>
    </section>
  );
}
