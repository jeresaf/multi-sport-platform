"use client";

// Members & roles data: the paged lists and what a change makes stale. A member change can revoke invitations
// (an inviter who leaves takes theirs, Q20) and change the user's own roles, so it refreshes all of them. Members and
// invitations are the tenant's staff users (S04 D13), so a change refreshes their usage too.
import type { components } from "@msp/api-client";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import type { QuotaUsageValue } from "@/components/quota-usage";
import { meQueryKey } from "@/lib/account";
import { call } from "@/lib/api";
import { formatQuantity } from "@/lib/entitlements";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { useTenantSubscription } from "../subscription/subscription-data";

export type Member = components["schemas"]["MemberResponse"];
export type MembershipStatus = components["schemas"]["MembershipStatus"];
export type Invitation = components["schemas"]["InvitationResponse"];
export type InvitationStatus = components["schemas"]["InvitationState"];

export function useMembers(status: MembershipStatus) {
  const { tenant, client } = useTenantWorkspace();
  return useInfiniteQuery({
    queryKey: ["members", tenant.tenantId, status],
    queryFn: ({ pageParam }) =>
      call(
        client.GET("/api/admin/v1/members", {
          params: { query: { status, cursor: pageParam ?? undefined } },
        }),
      ),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.page.hasMore ? last.page.nextCursor : undefined),
  });
}

/** `undefined`: the API's default, pending invitations with the expired ones. */
export function useInvitations(status: InvitationStatus | undefined) {
  const { tenant, client } = useTenantWorkspace();
  return useInfiniteQuery({
    queryKey: ["invitations", tenant.tenantId, status ?? "OPEN"],
    queryFn: ({ pageParam }) =>
      call(
        client.GET("/api/admin/v1/invitations", {
          params: { query: { status, cursor: pageParam ?? undefined } },
        }),
      ),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.page.hasMore ? last.page.nextCursor : undefined),
  });
}

/** Reads again everything a member or invitation change can affect, the user's own roles included. */
export function useRefreshMembership() {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["members", tenant.tenantId] }),
      queryClient.invalidateQueries({ queryKey: ["invitations", tenant.tenantId] }),
      queryClient.invalidateQueries({ queryKey: ["capabilities", "tenant", tenant.tenantId] }),
      queryClient.invalidateQueries({ queryKey: ["tenant-subscription", tenant.tenantId] }),
      queryClient.invalidateQueries({ queryKey: meQueryKey }),
    ]);
}

/**
 * The tenant's staff users against its plan (`MAX_STAFF_USERS`, S04 D13), from the Subscription read; undefined while it
 * loads or for a user who can't read it (the server still refuses an invitation past the limit).
 */
export function useStaffUsage(): QuotaUsageValue | undefined {
  const subscription = useTenantSubscription();
  return subscription.data?.usage.find((usage) => usage.code === "MAX_STAFF_USERS");
}

/** "Your plan allows 5 staff users." once every seat is taken (as the API's refusal says), else null. */
export function staffLimitReason(usage: QuotaUsageValue | undefined): string | null {
  if (!usage?.measured || usage.used === null || usage.limit === null || usage.used < usage.limit) {
    return null;
  }
  return `Your plan allows ${formatQuantity(usage.limit, "staff users")}.`;
}

/** Only an owner grants, takes away or invites with TENANT_OWNER (S03 D17); the screen offers it to owners only. */
export const ownerRole = "TENANT_OWNER";
