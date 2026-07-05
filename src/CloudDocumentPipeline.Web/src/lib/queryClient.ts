import { QueryClient } from "@tanstack/react-query";

// Shared client for server state, cache invalidation, and retry behavior.
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false
    }
  }
});
