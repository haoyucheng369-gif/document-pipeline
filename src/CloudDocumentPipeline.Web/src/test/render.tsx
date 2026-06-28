import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, type RenderOptions } from "@testing-library/react";
import type { PropsWithChildren, ReactElement } from "react";
import { MemoryRouter } from "react-router-dom";
import { ToastProvider } from "../components/ToastProvider";

// 测试渲染工具：
// 给页面测试统一包上 QueryClient、Router、Toast 这些真实运行时依赖，
// 避免每个测试都手写重复样板代码。
export function renderWithProviders(
  ui: ReactElement,
  {
    route = "/"
  }: {
    route?: string;
  } & Omit<RenderOptions, "wrapper"> = {}
) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false
      },
      mutations: {
        retry: false
      }
    }
  });

  function Wrapper({ children }: PropsWithChildren) {
    return (
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <MemoryRouter initialEntries={[route]}>{children}</MemoryRouter>
        </ToastProvider>
      </QueryClientProvider>
    );
  }

  return render(ui, { wrapper: Wrapper });
}
