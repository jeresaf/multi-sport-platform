import { createApiClient } from "@msp/api-client";
import { headers } from "next/headers";
import { cache } from "react";
import { fetchSite, type SiteResult } from "./site";

/** The API origin as the Next.js server sees it (server-side only; never sent to the browser). */
function apiUrl(): string {
  return process.env.MSP_API_URL || "http://localhost:5080";
}

/**
 * The site for the current request, looked up once per render (metadata, layout and page share it).
 * The incoming `Host` is forwarded as is: public data is public (D7). Behind a production proxy this must read the
 * proxy's forwarded host instead (PROGRESS.md follow-up).
 */
export const getCurrentSite = cache(async (): Promise<SiteResult> => {
  const requestHeaders = await headers();
  return fetchSite(createApiClient({ baseUrl: apiUrl() }), requestHeaders.get("host"));
});
