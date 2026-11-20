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

/** The tenant's IANA time zone; UTC while the settings load, or when the user can't read them. */
export function useTenantTimeZone(): string {
  return useTenantSettings().data?.timeZone ?? "UTC";
}

/**
 * The tenant's IANA time zone for a page that waits for it (review #93 Q1): `isLoading` while the settings are read
 * the first time, or read again after a failed read (re-review N6), so a date is never shown in UTC and then in the
 * tenant's zone (which can be another day). UTC when the user can't read the settings or their read failed.
 */
export function useTenantTimeZoneOnceRead(): { timeZone: string; isLoading: boolean } {
  const settings = useTenantSettings();
  return {
    timeZone: settings.data?.timeZone ?? "UTC",
    isLoading: settings.isLoading || (settings.isError && settings.isFetching),
  };
}

/** A date and time as people read them in a time zone: "20 Oct 2026, 14:30". */
export function formatDateTime(instant: string, timeZone: string): string {
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short", timeZone }).format(new Date(instant));
}

/** A date as people read it in a time zone: "20 Oct 2026". */
export function formatDate(instant: string, timeZone: string): string {
  return new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric", timeZone }).format(
    new Date(instant),
  );
}

/** Today's date in a time zone, as the API writes dates ("2026-11-20"): the tenant's date (S06 D13). */
export function todayIn(timeZone: string, now: Date = new Date()): string {
  // en-CA writes dates as YYYY-MM-DD.
  return new Intl.DateTimeFormat("en-CA", { year: "numeric", month: "2-digit", day: "2-digit", timeZone }).format(now);
}
