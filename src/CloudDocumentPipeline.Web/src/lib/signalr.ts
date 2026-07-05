import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from "@microsoft/signalr";
import { getRuntimeApiBaseUrl } from "./runtimeConfig";

const API_BASE_URL = getRuntimeApiBaseUrl();

type JobUpdatedPayload = {
  jobId: string;
  status: string;
  retryCount: number;
};

let connection: HubConnection | null = null;
let startPromise: Promise<HubConnection> | null = null;

// Share one SignalR connection across pages so list and detail views do not duplicate subscriptions.
async function ensureConnection() {
  if (connection) {
    if (connection.state === HubConnectionState.Disconnected && !startPromise) {
      startPromise = connection.start().then(() => connection!);
      await startPromise.finally(() => {
        startPromise = null;
      });
    }

    return connection;
  }

  connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/jobs`, {
      // The API uses wildcard CORS in local/testbed settings, so credentials must stay disabled.
      withCredentials: false
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  // Allow longer pauses during backend debugging without forcing frequent reconnects.
  connection.serverTimeoutInMilliseconds = 120_000;
  connection.keepAliveIntervalInMilliseconds = 15_000;

  startPromise = connection.start().then(() => connection!);
  await startPromise.finally(() => {
    startPromise = null;
  });

  return connection;
}

export async function subscribeToJobUpdates(
  handler: (payload: JobUpdatedPayload) => void
) {
  const hubConnection = await ensureConnection();
  hubConnection.on("jobUpdated", handler);

  return () => {
    hubConnection.off("jobUpdated", handler);
  };
}
