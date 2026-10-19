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
      return { title: "Page not found", robots: { index: false } };
    case "unavailable":
      return { title: "Site unavailable", robots: { index: false } };
    case "error":
      // The layout throws, so the error page renders with a 500.
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
    // A 500, not a 200 page, so crawlers retry instead of dropping the site. The message reaches the server log with
    // the error's digest, which the error page shows, so support can find the API's correlation ID.
    case "error":
      throw new Error(`The site lookup failed (correlation ID: ${result.correlationId ?? "none, no API response"}).`);
  }
}
