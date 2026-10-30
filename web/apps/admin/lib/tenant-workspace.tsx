"use client";

// The tenant workspace on screen, for its pages: the tenant the workspace layout rendered (from `/me`), a client that
// sends its `X-Tenant-Id`, the actions the user holds there, and why the ones their roles allow can't be used now. The
// layout renders pages only once all of them are known.
import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { CapabilityAccess } from "./access";
import { tenantApi } from "./api";
import { unavailableReason, type UnavailableAction } from "./unavailable";
import type { MeTenant } from "./workspace";
import { useWorkspaceNavigation } from "./workspace-context";

export type TenantWorkspace = {
  tenant: MeTenant;
  client: ReturnType<typeof tenantApi>;
  can: (action: string) => boolean;
  /**
   * Why an action the roles allow can't be used now ("Read-only: the subscription has ended"), or null. A control shows
   * when `can` or this says something: disabled, with the reason, in the second case (S04 D19).
   */
  unavailable: (action: string) => string | null;
};

type TenantContextValue = { tenant: MeTenant; unavailable: readonly UnavailableAction[]; access: CapabilityAccess };

// The layout hands its pages the tenant it rendered them for. A page that read `/me` itself could re-render with a
// newer `/me` before the layout does: after a member suspends or removes themselves the tenant is gone from it, and
// the page would fail where the layout is about to say "not found".
const TenantContext = createContext<TenantContextValue | null>(null);

export function TenantProvider({
  tenant,
  unavailable,
  access,
  children,
}: Readonly<TenantContextValue & { children: ReactNode }>) {
  const value = useMemo(() => ({ tenant, unavailable, access }), [tenant, unavailable, access]);
  return <TenantContext.Provider value={value}>{children}</TenantContext.Provider>;
}

export function useTenantWorkspace(): TenantWorkspace {
  const context = useContext(TenantContext);
  const { actions } = useWorkspaceNavigation();
  if (!context) {
    throw new Error("useTenantWorkspace needs the tenant workspace layout.");
  }

  const { tenant, unavailable, access } = context;
  const tenantId = tenant.tenantId;
  const client = useMemo(() => tenantApi(tenantId), [tenantId]);
  return {
    tenant,
    client,
    can: (action) => actions.includes(action),
    unavailable: (action) => unavailableReason(action, unavailable, access),
  };
}
