"use client";

import { useParams } from "next/navigation";
import { useEffect, useMemo, type ReactNode } from "react";
import { ProblemAlert } from "@/components/problem-alert";
import { AppShell } from "@/components/shell/app-shell";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BlockedTenant } from "@/components/workspace/blocked-tenant";
import { useMe } from "@/lib/account";
import { isApiProblem } from "@/lib/api";
import { useTenantCapabilities } from "@/lib/capabilities";
import { tenantNavigation, visibleNavigation } from "@/lib/navigation";
import { findTenant, isBlocked, rememberWorkspace, tenantPath } from "@/lib/workspace";
import { WorkspaceProvider } from "@/lib/workspace-context";

/**
 * A tenant's workspace (`/t/{slug}`): one of the user's tenants from `/me`, its navigation filtered by the user's
 * capabilities there. An unknown slug is a 404; a suspended or archived tenant says why; a refusal says why.
 */
export default function TenantWorkspaceLayout({ children }: Readonly<{ children: ReactNode }>) {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  const me = useMe();
  const tenant = me.data ? findTenant(me.data, tenantSlug) : undefined;
  const blocked = tenant ? isBlocked(tenant) : false;
  const capabilities = useTenantCapabilities(tenant?.tenantId ?? "", tenant !== undefined && !blocked);
  const items = useMemo(() => tenantNavigation(tenantSlug), [tenantSlug]);

  useEffect(() => {
    if (tenant) {
      rememberWorkspace(tenantPath(tenant.slug));
    }
  }, [tenant]);

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
        {capabilities.isPending && !blocked ? <p className="text-muted-foreground">Loading…</p> : null}
        {capabilities.isError && isApiProblem(capabilities.error) && capabilities.error.status === 403 ? (
          <ForbiddenView error={capabilities.error} />
        ) : capabilities.isError ? (
          <ProblemAlert error={capabilities.error} />
        ) : null}
      </AppShell>
    );
  }

  const { actions } = capabilities.data;
  return (
    <WorkspaceProvider value={{ items, actions }}>
      <AppShell workspace={tenant.displayName} navigation={visibleNavigation(items, actions)}>
        {children}
      </AppShell>
    </WorkspaceProvider>
  );
}
