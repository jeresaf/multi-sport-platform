// The workspaces a signed-in user can open (`/me`): their tenants under `/t/{slug}` and, for platform staff, the
// platform console. `/` opens the last-used one when it is still theirs.
import type { components } from "@msp/api-client";

export type Me = components["schemas"]["MeResponse"];
export type MeTenant = components["schemas"]["MeTenantResponse"];

export const platformPath = "/platform";

export function tenantPath(slug: string): string {
  return `/t/${slug}`;
}

/** Suspended and archived tenants answer only `tenant.access.read` (S03 D11): their workspace says why instead. */
export function isBlocked(tenant: MeTenant): boolean {
  return tenant.status === "SUSPENDED" || tenant.status === "ARCHIVED";
}

export function findTenant(me: Me, slug: string): MeTenant | undefined {
  return me.tenants.find((tenant) => tenant.slug === slug);
}

/** Where `/` goes: the last-used workspace, the first active tenant, the platform console, any tenant, or nowhere. */
export function landingPath(me: Me, lastUsed: string | null): string | null {
  const isPlatformStaff = me.platformRoles.length > 0;
  if (lastUsed === platformPath && isPlatformStaff) {
    return platformPath;
  }
  if (lastUsed && me.tenants.some((tenant) => tenantPath(tenant.slug) === lastUsed)) {
    return lastUsed;
  }

  const active = me.tenants.find((tenant) => tenant.status === "ACTIVE");
  if (active) {
    return tenantPath(active.slug);
  }
  if (isPlatformStaff) {
    return platformPath;
  }
  return me.tenants[0] ? tenantPath(me.tenants[0].slug) : null;
}

// The last-used workspace is a per-browser convenience: storage can be missing or refuse (private windows).
const lastUsedKey = "msp.admin.lastWorkspace";

export function readLastWorkspace(): string | null {
  try {
    return globalThis.localStorage?.getItem(lastUsedKey) ?? null;
  } catch {
    return null;
  }
}

export function rememberWorkspace(path: string): void {
  try {
    globalThis.localStorage?.setItem(lastUsedKey, path);
  } catch {
    // Not remembered: `/` falls back to the first active tenant.
  }
}

/**
 * Forgets the last-used workspace: on sign-out (the next person on this browser shouldn't learn it), or only `path`
 * when that workspace refuses the user (so `/` doesn't lead back to the refusal).
 */
export function forgetWorkspace(path?: string): void {
  try {
    if (path === undefined || globalThis.localStorage?.getItem(lastUsedKey) === path) {
      globalThis.localStorage?.removeItem(lastUsedKey);
    }
  } catch {
    // Nothing stored, or storage refuses: nothing to forget.
  }
}
