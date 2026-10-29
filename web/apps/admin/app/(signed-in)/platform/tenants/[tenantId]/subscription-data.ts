"use client";

// Platform console → a tenant's subscription (S04 D16): its periods, overrides, effective entitlements and usage. Every
// command answers with the whole subscription, which replaces the one shown.
import type { components } from "@msp/api-client";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, call } from "@/lib/api";
import { tenantsQueryKey } from "../tenants-data";

export type Subscription = components["schemas"]["SubscriptionResponse"];
export type SubscriptionPeriod = components["schemas"]["SubscriptionPeriodResponse"];
export type SubscriptionKind = components["schemas"]["SubscriptionKind"];
export type EntitlementOverride = components["schemas"]["EntitlementOverrideResponse"];

export const subscriptionKinds: readonly SubscriptionKind[] = ["PAID", "TRIAL", "COMPLIMENTARY"];

export const subscriptionQueryKey = (tenantId: string) => [...tenantsQueryKey, "subscription", tenantId] as const;

export function useSubscription(tenantId: string, enabled: boolean) {
  return useQuery({
    enabled,
    queryKey: subscriptionQueryKey(tenantId),
    queryFn: () =>
      call(api.GET("/api/platform/v1/tenants/{tenantId}/subscription", { params: { path: { tenantId } } })),
  });
}

/** Shows the subscription a command answered with. */
export function useSubscriptionSaved() {
  const queryClient = useQueryClient();
  return (subscription: Subscription) =>
    queryClient.setQueryData(subscriptionQueryKey(subscription.tenantId), subscription);
}

/** The period that decides access, and the next scheduled one. */
export function currentAndNext(subscription: Subscription) {
  const byId = (id: string | null) => subscription.periods.find((period) => period.id === id);
  return { current: byId(subscription.currentPeriodId), next: byId(subscription.nextPeriodId) };
}

/**
 * Whether a period is in effect today, so an active tenant has full access (S04 D8): the current period is ACTIVE,
 * EXPIRING or GRACE. Without one (none, scheduled only, expired, cancelled) an active tenant is read-only.
 */
export function inEffectToday(subscription: Subscription): boolean {
  const { current } = currentAndNext(subscription);
  return current?.status === "ACTIVE" || current?.status === "EXPIRING" || current?.status === "GRACE";
}

/** A period that cancelling would end (state-machines.md): not cancelled, not expired. */
export function isCancellable(period: SubscriptionPeriod): boolean {
  return period.status !== "CANCELLED" && period.status !== "EXPIRED";
}
