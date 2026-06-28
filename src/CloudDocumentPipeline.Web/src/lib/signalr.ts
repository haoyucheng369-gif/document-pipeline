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

async function ensureConnection() {
  if (connection) {
    // 已经有连接实例时，只有断开状态才尝试重连。
    if (connection.state === HubConnectionState.Disconnected && !startPromise) {
      startPromise = connection.start().then(() => connection!);
      await startPromise.finally(() => {
        startPromise = null;
      });
    }

    return connection;
  }

  // 创建全局唯一的 SignalR 连接，列表页和详情页共用这一条连接。
  connection = new HubConnectionBuilder()
    // 测试环境后端当前使用 wildcard CORS，这里关闭 credentials，
    // 避免 SignalR negotiate 请求因为浏览器的 CORS 规则被拦截。
    .withUrl(`${API_BASE_URL}/hubs/jobs`, {
      withCredentials: false
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  // 调试时如果在后端打断点，连接可能长时间收不到心跳。
  // 这里把客户端超时放宽，减少因为调试暂停导致的误断线。
  connection.serverTimeoutInMilliseconds = 120_000;
  connection.keepAliveIntervalInMilliseconds = 15_000;

  startPromise = connection.start().then(() => connection!);
  await startPromise.finally(() => {
    startPromise = null;
  });

  return connection;
}

// 统一订阅 Job 状态更新事件，供列表页和详情页复用。
export async function subscribeToJobUpdates(
  handler: (payload: JobUpdatedPayload) => void
) {
  const hubConnection = await ensureConnection();
  hubConnection.on("jobUpdated", handler);

  return () => {
    hubConnection.off("jobUpdated", handler);
  };
}
