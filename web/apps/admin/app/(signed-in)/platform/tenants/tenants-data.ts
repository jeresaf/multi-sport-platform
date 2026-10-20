"use client";

// Platform console → Tenants data: the paged list and one tenant. A change to a tenant updates its detail and makes
// the list stale.
import type { components } from "@msp/api-client";
import { useInfiniteQuery, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, call } from "@/lib/api";

export type Tenant = components["schemas"]["TenantResponse"];
export type TenantListItem = components["schemas"]["TenantListItem"];
export type TenantStatus = components["schemas"]["TenantStatus"];

export const tenantsQueryKey = ["platform", "tenants"] as const;
const listQueryKey = (status: TenantStatus | undefined) => [...tenantsQueryKey, "list", status ?? "ALL"] as const;
export const tenantQueryKey = (tenantId: string) => [...tenantsQueryKey, "detail", tenantId] as const;

/** `undefined`: every status. */
export function useTenants(status: TenantStatus | undefined) {
  return useInfiniteQuery({
    queryKey: listQueryKey(status),
    queryFn: ({ pageParam }) =>
      call(
        api.GET("/api/platform/v1/tenants", {
          params: { query: { status, cursor: pageParam ?? undefined } },
        }),
      ),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.page.hasMore ? last.page.nextCursor : undefined),
  });
}

export function useTenant(tenantId: string) {
  return useQuery({
    queryKey: tenantQueryKey(tenantId),
    queryFn: () => call(api.GET("/api/platform/v1/tenants/{tenantId}", { params: { path: { tenantId } } })),
  });
}

/** Shows a tenant as the API returned it after a change, and reads the list again. */
export function useTenantSaved() {
  const queryClient = useQueryClient();
  return (tenant: Tenant) => {
    queryClient.setQueryData(tenantQueryKey(tenant.id), tenant);
    return queryClient.invalidateQueries({ queryKey: [...tenantsQueryKey, "list"] });
  };
}
