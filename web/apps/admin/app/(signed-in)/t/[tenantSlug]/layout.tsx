"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useEffect, useMemo, type ReactNode } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { AppShell } from "@/components/shell/app-shell";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { AccessBanner } from "@/components/workspace/access-banner";
import { BlockedTenant } from "@/components/workspace/blocked-tenant";
import { meQueryKey, useMe } from "@/lib/account";
import { isApiProblem, isUnauthenticated } from "@/lib/api";
import { useTenantCapabilities } from "@/lib/capabilities";
import { tenantNavigation, visibleNavigation } from "@/lib/navigation";
import { TenantProvider } from "@/lib/tenant-workspace";
import { findTenant, forgetWorkspace, isBlocked, rememberWorkspace, tenantPath } from "@/lib/workspace";
import { WorkspaceProvider } from "@/lib/workspace-context";

/**
 * A tenant's workspace (`/t/{slug}`): one of the user's tenants from `/me`, its navigation filtered by the user's
 * capabilities there. An unknown slug is a 404; a suspended or archived tenant says why; a refusal says why. Every
 * member sees the banner for an ending subscription or a read-only workspace (S04 D19).
 */
export default function TenantWorkspaceLayout({ children }: Readonly<{ children: ReactNode }>) {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  const me = useMe();
  const tenant = me.data ? findTenant(me.data, tenantSlug) : undefined;
  const blocked = tenant ? isBlocked(tenant) : false;
  const capabilities = useTenantCapabilities(tenant?.tenantId ?? "", tenant !== undefined && !blocked);
  const items = useMemo(() => tenantNavigation(tenantSlug), [tenantSlug]);

  const queryClient = useQueryClient();
  const refusal = !blocked && isApiProblem(capabilities.error) && capabilities.error.status === 403 ? capabilities.error : null;

  // Only a workspace that opens (or says why it's closed) is remembered, and one that refuses is forgotten: `/` must
  // not lead back to a refusal. A tenant blocked since `me` was read asks again, so the page says why it's closed.
  const opens = blocked || capabilities.isSuccess;
  useEffect(() => {
    if (tenant && opens) {
      rememberWorkspace(tenantPath(tenant.slug));
    }
  }, [tenant, opens]);
  useEffect(() => {
    if (!tenant || !refusal) {
      return;
    }
    forgetWorkspace(tenantPath(tenant.slug));
    if (refusal.problem.code === "TENANT_BLOCKED") {
      void queryClient.invalidateQueries({ queryKey: meQueryKey });
    }
  }, [tenant, refusal, queryClient]);

  if (!tenant) {
    return (
      <AppShell workspace="Choose a workspace" navigation={[]}>
        <NotFoundView />
      </AppShell>
    );
  }

  if (blocked || !capabilities.data) {
    return (
      <AppShell workspace={tenant.displayName} navigation={[]}>
        {blocked ? <BlockedTenant tenant={tenant} /> : null}
        {/* A 401 is the guard's: it sends the user to sign in. */}
        {!blocked && (capabilities.isPending || isUnauthenticated(capabilities.error)) ? <Loading /> : null}
        {refusal ? (
          <ForbiddenView error={refusal} />
        ) : !blocked && capabilities.isError && !isUnauthenticated(capabilities.error) ? (
          <ProblemAlert error={capabilities.error} />
        ) : null}
      </AppShell>
    );
  }

  const { actions, access } = capabilities.data;
  return (
    <WorkspaceProvider value={{ items, actions }}>
      <AppShell workspace={tenant.displayName} navigation={visibleNavigation(items, actions)}>
        <AccessBanner access={access} />
        <TenantProvider tenant={tenant}>{children}</TenantProvider>
      </AppShell>
    </WorkspaceProvider>
  );
}
