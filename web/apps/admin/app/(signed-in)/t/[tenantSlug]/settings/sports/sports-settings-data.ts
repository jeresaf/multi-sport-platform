"use client";

// Tenant settings → Enabled sports data (S05 D15, D17): every published variant and whether the tenant has it enabled,
// with the sport quota's usage for those who may read the subscription. Enable and disable answer the list as it is
// after the change, so it replaces the cached one; the Subscription page's usage is read again.
import type { components } from "@msp/api-client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { QuotaUsageValue } from "@/components/quota-usage";
import { call } from "@/lib/api";
import { formatQuantity } from "@/lib/entitlements";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

export type TenantSports = components["schemas"]["TenantSportsResponse"];
export type TenantSport = components["schemas"]["TenantSportResponse"];

/** The quota's unit, as the entitlement catalogue names it (`MAX_ENABLED_SPORTS`). */
export const sportsUnit = "sports";

function useTenantSportsKey() {
  const { tenant } = useTenantWorkspace();
  return ["tenant-sports", tenant.tenantId] as const;
}

/** `needed`: false when the screen has no use for the list now (the person page asks only to offer a profile). */
export function useTenantSports(needed = true) {
  const { client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: useTenantSportsKey(),
    queryFn: () => call(client.GET("/api/admin/v1/tenant/sports")),
    enabled: can("tenant.sport.read") && needed,
  });
}

/** Reads the list and the Subscription page's usage again (after a refusal that says the list was out of date). */
export function useRefreshTenantSports() {
  const { tenant } = useTenantWorkspace();
  const key = useTenantSportsKey();
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: key }),
      queryClient.invalidateQueries({ queryKey: ["tenant-subscription", tenant.tenantId] }),
    ]);
}

/** Keeps the list the change answered and marks the Subscription page's usage stale. */
function useSaveAnswer() {
  const { tenant } = useTenantWorkspace();
  const key = useTenantSportsKey();
  const queryClient = useQueryClient();
  return (answer: TenantSports) => {
    queryClient.setQueryData(key, answer);
    return queryClient.invalidateQueries({ queryKey: ["tenant-subscription", tenant.tenantId] });
  };
}

export function useEnableSport() {
  const { client } = useTenantWorkspace();
  const save = useSaveAnswer();
  return useMutation({
    mutationFn: (sport: TenantSport) =>
      call(client.POST("/api/admin/v1/tenant/sports/{variant}/enable", { params: { path: { variant: sport.variant } } })),
    onSuccess: save,
  });
}

export function useDisableSport() {
  const { client } = useTenantWorkspace();
  const save = useSaveAnswer();
  return useMutation({
    mutationFn: ({ sport, reason }: { sport: TenantSport; reason: string }) =>
      call(
        client.POST("/api/admin/v1/tenant/sports/{variant}/disable", {
          params: { path: { variant: sport.variant } },
          body: { reason },
        }),
      ),
    onSuccess: save,
  });
}

/**
 * "Your plan allows 3 sports." when enabling `sport` would pass the limit (as the API's refusal says), else null. The
 * quota counts sports, not variants (S05 D13): a variant of a sport the tenant already has adds nothing, so it is never
 * refused. Without the usage (no `tenant.subscription.read`) nothing is known; the server still refuses.
 */
export function sportLimitReason(
  sport: TenantSport,
  variants: readonly TenantSport[],
  usage: QuotaUsageValue | null | undefined,
): string | null {
  if (!usage?.measured || usage.used === null || usage.limit === null || usage.used < usage.limit) {
    return null;
  }
  if (variants.some((other) => other.enabled && other.sport === sport.sport)) {
    return null;
  }
  return `Your plan allows ${formatQuantity(usage.limit, sportsUnit)}.`;
}
