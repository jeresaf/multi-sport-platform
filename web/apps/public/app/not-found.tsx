import type { Metadata } from "next";
import { StatusPage } from "@/components/status-page";

export const metadata: Metadata = { title: "Page not found", robots: { index: false } };

// Unknown host (no tenant serves it) or an unknown path, so the wording fits both.
export default function NotFound() {
  return (
    <StatusPage title="We can't find this page">
      <p>Check the address and try again.</p>
    </StatusPage>
  );
}
