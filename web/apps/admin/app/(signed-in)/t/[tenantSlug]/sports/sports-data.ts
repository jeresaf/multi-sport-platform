"use client";

// Tenant workspace → Sports data (S05 D16): the published variants and a variant's definition. Definitions change only
// by publishing a new version (D4), so a read is kept for the session.
import { useQuery } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

/** Not read without `sport.read` (`enabled`): the request would only be refused. */
export function useSportVariants() {
  const { client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["sports", "variants"],
    queryFn: () => call(client.GET("/api/admin/v1/sports")),
    enabled: can("sport.read"),
    staleTime: Infinity,
  });
}

/** A variant's definition: the latest version, or `version` when the address names one. */
export function useSportDefinition(variant: string, version?: number) {
  const { client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["sports", "definition", variant, version ?? "latest"],
    queryFn: () =>
      call(client.GET("/api/admin/v1/sports/{variant}", { params: { path: { variant }, query: { version } } })),
    enabled: can("sport.read"),
    staleTime: Infinity,
  });
}
