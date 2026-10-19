// Which tenant's site this request is for (S02 D7): the public app sends the request host to the API as
// `X-Forwarded-Host` and the API answers with the tenant, `TENANT_NOT_FOUND` or `TENANT_UNAVAILABLE`.
import { isProblemDetails, type ApiClient, type components } from "@msp/api-client";

export type Site = components["schemas"]["SiteResponse"];

export type SiteResult =
  | { kind: "found"; site: Site }
  | { kind: "not-found" }
  | { kind: "unavailable" }
  /** The API failed or couldn't be reached; `correlationId` is set when the API answered with Problem Details. */
  | { kind: "error"; correlationId?: string };

const timeoutMs = 5_000;

/** The host name of a `Host` header value, lower case and without the port (`KMSA.localhost:3000` → `kmsa.localhost`). */
export function hostName(hostHeader: string | null | undefined): string | undefined {
  const value = hostHeader?.trim().toLowerCase();
  if (!value) {
    return undefined;
  }

  // An IPv6 literal keeps its brackets: `[::1]:3000` → `[::1]`.
  const name = value.startsWith("[") ? value.slice(0, value.indexOf("]") + 1) : value.split(":")[0];
  return name || undefined;
}

export async function fetchSite(client: ApiClient, hostHeader: string | null | undefined): Promise<SiteResult> {
  const host = hostName(hostHeader);
  if (!host) {
    return { kind: "not-found" };
  }

  try {
    const { data, error, response } = await client.GET("/api/public/v1/site", {
      headers: { "X-Forwarded-Host": host },
      // Responses differ per host; the API caches the lookup, so the app doesn't keep its own copy.
      cache: "no-store",
      signal: AbortSignal.timeout(timeoutMs),
    });

    if (data) {
      return { kind: "found", site: data };
    }

    const code = isProblemDetails(error) ? error.code : undefined;
    if (response.status === 404 && code === "TENANT_NOT_FOUND") {
      return { kind: "not-found" };
    }
    if (response.status === 403 && code === "TENANT_UNAVAILABLE") {
      return { kind: "unavailable" };
    }

    return { kind: "error", correlationId: isProblemDetails(error) ? error.correlationId : undefined };
  } catch {
    // Network failure or timeout: there is no response to take a correlation ID from.
    return { kind: "error" };
  }
}
