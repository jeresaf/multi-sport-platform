"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

export function Providers({ children }: Readonly<{ children: ReactNode }>) {
  // One client per browser session; created in state so a re-render never replaces it.
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          // An API refusal is an answer, not a glitch: show it instead of asking again.
          queries: { retry: false, refetchOnWindowFocus: false },
          mutations: { retry: false },
        },
      }),
  );

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
