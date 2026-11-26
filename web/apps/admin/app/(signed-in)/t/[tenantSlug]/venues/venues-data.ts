"use client";

// Registry → Venues data (S07 D19, D23): the directory, a venue, its status history and the teams that call it home.
// Every field a venue read answers is public or internal; none is personal, so nothing here is read on its own.
import type { components } from "@msp/api-client";
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

type Schemas = components["schemas"];
export type VenueListItem = Schemas["VenueListItem"];
export type Venue = Schemas["VenueResponse"];
export type VenueStatus = Schemas["VenueStatus"];
export type VenueType = Schemas["VenueType"];
export type VenueSurface = Schemas["VenueSurface"];

export const venueStatuses: readonly VenueStatus[] = ["ACTIVE", "TEMPORARILY_UNAVAILABLE", "CLOSED", "ARCHIVED"];

/** The directory's search and filters, as the address holds them; a missing one is the API's default. */
export type VenueFilters = {
  q?: string;
  city?: string;
  status?: VenueStatus;
  variant?: string;
};

/** The filters from the address (`?q=&city=&status=&variant=`); an unknown status is left out. */
export function venueFilters(params: URLSearchParams): VenueFilters {
  return {
    q: params.get("q")?.trim() || undefined,
    city: params.get("city")?.trim() || undefined,
    status: venueStatuses.find((status) => status === params.get("status")),
    variant: params.get("variant") || undefined,
  };
}

/** None of these reads is asked without `venue.read` (`enabled`): it would only be refused. */
export function useVenues(filters: VenueFilters) {
  const { tenant, client, can } = useTenantWorkspace();
  return useInfiniteQuery({
    queryKey: ["venues", tenant.tenantId, filters],
    queryFn: ({ pageParam }) =>
      call(client.GET("/api/admin/v1/venues", { params: { query: { ...filters, cursor: pageParam ?? undefined } } })),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.page.hasMore ? last.page.nextCursor : undefined),
    enabled: can("venue.read"),
  });
}

export function venueQueryKey(tenantId: string, venueId: string) {
  return ["venue", tenantId, venueId] as const;
}

export function useVenue(venueId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: venueQueryKey(tenant.tenantId, venueId),
    queryFn: () => call(client.GET("/api/admin/v1/venues/{venueId}", { params: { path: { venueId } } })),
    enabled: can("venue.read"),
  });
}

export function venueStatusChangesQueryKey(tenantId: string, venueId: string) {
  return ["venue-status-changes", tenantId, venueId] as const;
}

export function useVenueStatusChanges(venueId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: venueStatusChangesQueryKey(tenant.tenantId, venueId),
    queryFn: () =>
      call(client.GET("/api/admin/v1/venues/{venueId}/status-changes", { params: { path: { venueId } } })),
    enabled: can("venue.read"),
  });
}

/**
 * The teams whose home venue this is (the Teams tab), every status but archived, in the team directory's order. Asked
 * only with `team.read`: a role that reads venues but not teams sees the tab say so.
 */
export function useHomeTeams(venueId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useInfiniteQuery({
    queryKey: ["teams", tenant.tenantId, { homeVenueId: venueId }],
    queryFn: ({ pageParam }) =>
      call(
        client.GET("/api/admin/v1/teams", {
          params: { query: { homeVenueId: venueId, cursor: pageParam ?? undefined } },
        }),
      ),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.page.hasMore ? last.page.nextCursor : undefined),
    enabled: can("team.read"),
  });
}

/**
 * The statuses a venue may move to from its own (state-machines.md, Venue): `ACTIVE ⇄ TEMPORARILY_UNAVAILABLE`, either
 * → `CLOSED`, `CLOSED → ACTIVE` (reopen), any but `ARCHIVED` → `ARCHIVED`. Unarchiving goes back to the status before
 * archiving, which only the history knows (`unarchiveTo`).
 */
export function venueMovesFrom(status: VenueStatus): VenueStatus[] {
  switch (status) {
    case "ACTIVE":
      return ["TEMPORARILY_UNAVAILABLE", "CLOSED", "ARCHIVED"];
    case "TEMPORARILY_UNAVAILABLE":
      return ["ACTIVE", "CLOSED", "ARCHIVED"];
    case "CLOSED":
      return ["ACTIVE", "ARCHIVED"];
    case "ARCHIVED":
      return [];
  }
}

type VenueStatusChange = Schemas["VenueStatusChangeResponse"];

/** The status an archived venue goes back to: the one it left for its latest archiving (the history is newest first). */
export function unarchiveTo(changes: readonly VenueStatusChange[]): VenueStatus | null {
  const archived = changes.find((change) => change.toStatus === "ARCHIVED");
  return (archived?.fromStatus as VenueStatus | null | undefined) ?? null;
}

/** The venue as a dialog first showed it: what a change is based on. */
export type OpenedVenue = Pick<Venue, "id" | "version">;

/** Keeps the venue a change answered, and marks the directory and the history stale. */
export function useKeepVenue() {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return (venue: Venue) => {
    queryClient.setQueryData(venueQueryKey(tenant.tenantId, venue.id), venue);
    void queryClient.invalidateQueries({ queryKey: ["venues", tenant.tenantId] });
    void queryClient.invalidateQueries({ queryKey: venueStatusChangesQueryKey(tenant.tenantId, venue.id) });
  };
}

/** Reads the venue again (after a concurrency conflict). */
export function useReloadVenue(venueId: string) {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: venueQueryKey(tenant.tenantId, venueId) });
}

/** Moves the venue's status from the version the dialog opened on (`venue.status.change`, a reason required). */
export function useChangeVenueStatus(venue: OpenedVenue) {
  const { client } = useTenantWorkspace();
  const keep = useKeepVenue();
  return useMutation({
    mutationFn: ({ status, reason }: { status: VenueStatus; reason: string }) =>
      call(
        client.POST("/api/admin/v1/venues/{venueId}/status", {
          params: { path: { venueId: venue.id }, header: ifMatch(venue.version) },
          body: { status, reason },
        }),
      ),
    onSuccess: keep,
  });
}
