"use client";

// Tenant settings → Subscription (S04 D15): the tenant's plan as its members see it, from
// `GET /api/admin/v1/tenant/subscription`. No reasons or names: those are the platform's.
import type { components } from "@msp/api-client";
import { useQuery } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

export type TenantSubscription = components["schemas"]["TenantSubscriptionResponse"];
export type TenantSubscriptionPeriod = components["schemas"]["TenantSubscriptionPeriod"];

export function useTenantSubscription() {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["tenant-subscription", tenant.tenantId],
    queryFn: () => call(client.GET("/api/admin/v1/tenant/subscription")),
    enabled: can("tenant.subscription.read"),
  });
}
