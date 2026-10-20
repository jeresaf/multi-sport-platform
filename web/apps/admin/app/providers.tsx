"use client";

import { QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";
import { createQueryClient } from "@/lib/query-client";

export function Providers({ children }: Readonly<{ children: ReactNode }>) {
  // One client per browser session; created in state so a re-render never replaces it.
  const [queryClient] = useState(createQueryClient);

  // Marks the page interactive. Until React hydrates, a form submits natively (a GET reload that loses the input);
  // the e2e tests wait for this before typing (e2e/tests/support/pages.ts).
  useEffect(() => {
    document.documentElement.dataset.hydrated = "";
  }, []);

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
