"use client";

import { StatusPage } from "@/components/status-page";

// A bug while rendering. Expected API failures are handled by the site layout, which shows the correlation ID.
export default function ErrorPage({ error }: { error: Error & { digest?: string } }) {
  return (
    <StatusPage title="Something went wrong" correlationId={error.digest}>
      <p>Please try again in a moment.</p>
    </StatusPage>
  );
}
