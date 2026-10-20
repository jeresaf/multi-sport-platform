// The admin app's client: same-origin calls through the app's `/api/*` rewrite, so the session cookie is first-party
// (S03 D5). Non-GET requests carry `X-CSRF: 1` (D7); requests in a tenant workspace carry `X-Tenant-Id`.
import type { Middleware } from "openapi-fetch";
import { createApiClient, type ApiClientOptions } from "./index";

export const csrfHeader = "X-CSRF";
export const tenantHeader = "X-Tenant-Id";

const safeMethods = new Set(["GET", "HEAD", "OPTIONS"]);

export type AdminClientOptions = Partial<Omit<ApiClientOptions, "headers">> & {
  /** The tenant workspace's tenant; every request then carries it. Omit for account and platform calls. */
  tenantId?: string;
};

/** Adds the CSRF header to state-changing requests and, when given, the tenant header to every request. */
export function adminHeaders(tenantId?: string): Middleware {
  return {
    onRequest({ request }) {
      if (!safeMethods.has(request.method.toUpperCase())) {
        request.headers.set(csrfHeader, "1");
      }
      if (tenantId) {
        request.headers.set(tenantHeader, tenantId);
      }
      return request;
    },
  };
}

export function createAdminClient({ tenantId, baseUrl = "", ...options }: AdminClientOptions = {}) {
  const client = createApiClient({ credentials: "same-origin", ...options, baseUrl });
  client.use(adminHeaders(tenantId));
  return client;
}
