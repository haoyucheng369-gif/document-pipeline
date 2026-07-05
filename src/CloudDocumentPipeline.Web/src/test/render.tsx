import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, type RenderOptions } from "@testing-library/react";
import type { PropsWithChildren, ReactElement } from "react";
import { MemoryRouter } from "react-router-dom";
import { ToastProvider } from "../components/ToastProvider";

// Test helper that wraps pages with the same Router, QueryClient, and Toast dependencies as runtime.
export function renderWithProviders(
  ui: ReactElement,
  {
    route = "/"
  }: {
    route?: string;
  } & Omit<RenderOptions, "wrapper"> = {}
) {
  // Tests disable retry so failures are deterministic and fast.
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
