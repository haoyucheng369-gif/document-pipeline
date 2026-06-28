import { QueryClient } from "@tanstack/react-query";

// QueryClient：
// 统一管理前端的服务端数据缓存、请求重试和刷新策略。
// 当前这个项目主要用它来管理任务列表、任务详情和重试后的缓存失效。
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false
    }
  }
});
