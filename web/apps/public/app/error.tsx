"use client";

import { StatusPage } from "@/components/status-page";

// A failed site lookup (the layout throws) or a bug while rendering. Next.js logs the error with its digest.
export default function ErrorPage({ error }: { error: Error & { digest?: string } }) {
  return (
    <StatusPage title="Something went wrong" reference={error.digest}>
      <p>Please try again in a moment.</p>
    </StatusPage>
  );
}
