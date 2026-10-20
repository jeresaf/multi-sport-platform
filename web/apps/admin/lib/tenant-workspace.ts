"use client";

// The tenant workspace on screen, for its pages: the tenant from `/me`, a client that sends its `X-Tenant-Id`, and
// the actions the user holds there. The workspace layout renders pages only once all three are known.
import { useParams } from "next/navigation";
import { useMemo } from "react";
import { useMe } from "./account";
import { tenantApi } from "./api";
import { findTenant, type MeTenant } from "./workspace";
import { useWorkspaceNavigation } from "./workspace-context";

export type TenantWorkspace = {
  tenant: MeTenant;
  client: ReturnType<typeof tenantApi>;
  can: (action: string) => boolean;
};

export function useTenantWorkspace(): TenantWorkspace {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  const me = useMe();
  const { actions } = useWorkspaceNavigation();
  const tenant = me.data ? findTenant(me.data, tenantSlug) : undefined;
  if (!tenant) {
    throw new Error("useTenantWorkspace needs the tenant workspace layout.");
  }

  const tenantId = tenant.tenantId;
  const client = useMemo(() => tenantApi(tenantId), [tenantId]);
  return { tenant, client, can: (action) => actions.includes(action) };
}
