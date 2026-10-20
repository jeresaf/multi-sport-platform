"use client";

// The workspace tenant's settings (`GET /api/admin/v1/tenant/settings`), read by the Organisation screen and by any
// screen that shows dates in the tenant's time zone.
import { useQuery } from "@tanstack/react-query";
import { call } from "./api";
import { useTenantWorkspace } from "./tenant-workspace";

export const settingsQueryKey = (tenantId: string) => ["tenant-settings", tenantId] as const;

export function useTenantSettings() {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: settingsQueryKey(tenant.tenantId),
    queryFn: () => call(client.GET("/api/admin/v1/tenant/settings")),
    enabled: can("tenant.settings.read"),
  });
}

/** The tenant's IANA time zone; UTC (and said so) when the user can't read the settings or they haven't loaded. */
export function useTenantTimeZone(): string {
  return useTenantSettings().data?.timeZone ?? "UTC";
}

/** A date as people read it in a time zone: "20 Oct 2026". */
export function formatDate(instant: string, timeZone: string): string {
  return new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric", timeZone }).format(
    new Date(instant),
  );
}
