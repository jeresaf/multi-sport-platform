"use client";

// Tenant workspace → Formats data (S05 D16): the published formats and a format. Formats change only by publishing a
// new version (D4), so a read is kept for the session.
import { useQuery } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

/** Not read without `format.read` (`enabled`): the request would only be refused. */
export function useFormats() {
  const { client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["formats", "list"],
    queryFn: () => call(client.GET("/api/admin/v1/formats")),
    enabled: can("format.read"),
    staleTime: Infinity,
  });
}

/** A format: the latest version, or `version` when the address names one; `enabled: false` skips it. */
export function useFormat(code: string, version?: number, enabled = true) {
  const { client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["formats", "format", code, version ?? "latest"],
    queryFn: () => call(client.GET("/api/admin/v1/formats/{code}", { params: { path: { code }, query: { version } } })),
    enabled: enabled && can("format.read"),
    staleTime: Infinity,
  });
}
