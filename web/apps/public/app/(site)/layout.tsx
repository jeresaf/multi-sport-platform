import type { Metadata } from "next";
import { notFound } from "next/navigation";
import type { ReactNode } from "react";
import { SiteShell } from "@/components/site-shell";
import { StatusPage } from "@/components/status-page";
import { getCurrentSite } from "@/lib/current-site";

// Pages for a tenant's site. The tenant comes from the request host, so every page renders per request.
export async function generateMetadata(): Promise<Metadata> {
  const result = await getCurrentSite();
  switch (result.kind) {
    case "found":
      return { title: result.site.displayName };
    case "not-found":
      return { title: "Site not found", robots: { index: false } };
    case "unavailable":
      return { title: "Site unavailable", robots: { index: false } };
    case "error":
      return { title: "Something went wrong", robots: { index: false } };
  }
}

export default async function SiteLayout({ children }: Readonly<{ children: ReactNode }>) {
  const result = await getCurrentSite();
  switch (result.kind) {
    case "found":
      return <SiteShell site={result.site}>{children}</SiteShell>;
    case "not-found":
      return notFound();
    // The owner chose a 200 page with noindex for an unavailable tenant (S02-T5b plan).
    case "unavailable":
      return (
        <StatusPage title="This site isn't available right now">
          <p>Please check back later.</p>
        </StatusPage>
      );
    case "error":
      return (
        <StatusPage title="Something went wrong" correlationId={result.correlationId}>
          <p>{"We couldn't load this site. Please try again in a moment."}</p>
        </StatusPage>
      );
  }
}
