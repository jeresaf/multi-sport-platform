"use client";

// The tenant workspace on screen, for its pages: the tenant the workspace layout rendered (from `/me`), a client that
// sends its `X-Tenant-Id`, and the actions the user holds there. The layout renders pages only once all three are known.
import { createContext, useContext, useMemo, type ReactNode } from "react";
import { tenantApi } from "./api";
import type { MeTenant } from "./workspace";
import { useWorkspaceNavigation } from "./workspace-context";

export type TenantWorkspace = {
  tenant: MeTenant;
  client: ReturnType<typeof tenantApi>;
  can: (action: string) => boolean;
};

// The layout hands its pages the tenant it rendered them for. A page that read `/me` itself could re-render with a
// newer `/me` before the layout does: after a member suspends or removes themselves the tenant is gone from it, and
// the page would fail where the layout is about to say "not found".
const TenantContext = createContext<MeTenant | null>(null);

export function TenantProvider({ tenant, children }: Readonly<{ tenant: MeTenant; children: ReactNode }>) {
  return <TenantContext.Provider value={tenant}>{children}</TenantContext.Provider>;
}

export function useTenantWorkspace(): TenantWorkspace {
  const tenant = useContext(TenantContext);
  const { actions } = useWorkspaceNavigation();
  if (!tenant) {
    throw new Error("useTenantWorkspace needs the tenant workspace layout.");
  }

  const tenantId = tenant.tenantId;
  const client = useMemo(() => tenantApi(tenantId), [tenantId]);
  return { tenant, client, can: (action) => actions.includes(action) };
}
