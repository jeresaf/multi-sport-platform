"use client";

// Registry → Venues data (S07 D19, D23): the directory, a venue, its status history and the teams that call it home.
// Every field a venue read answers is public or internal; none is personal, so nothing here is read on its own.
import type { components } from "@msp/api-client";
import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { NamesAndIdsApi } from "@/components/registry/names-and-ids/names-and-ids-data";
import { call, isApiProblem } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

type Schemas = components["schemas"];
export type VenueListItem = Schemas["VenueListItem"];
export type Venue = Schemas["VenueResponse"];
export type VenueStatus = Schemas["VenueStatus"];
export type VenueType = Schemas["VenueType"];
export type VenueSurface = Schemas["VenueSurface"];
export type CreateVenueRequest = Schemas["CreateVenueRequest"];
export type UpdateVenueRequest = Schemas["UpdateVenueRequest"];
export type CheckVenueDuplicatesRequest = Schemas["CheckVenueDuplicatesRequest"];

export const venueStatuses: readonly VenueStatus[] = ["ACTIVE", "TEMPORARILY_UNAVAILABLE", "CLOSED", "ARCHIVED"];

/** D3's venue types, in the form's order. */
export const venueTypes = [
  "STADIUM",
  "ARENA",
  "SPORTS_GROUND",
  "FIELD",
  "COURT",
  "INDOOR_HALL",
  "TRACK",
  "MULTI_SPORT_COMPLEX",
  "SCHOOL_GROUND",
  "OTHER",
] as const satisfies readonly VenueType[];

/** D3's surfaces, in the form's order. */
export const venueSurfaces = [
  "NATURAL_GRASS",
  "ARTIFICIAL_TURF",
  "HYBRID_GRASS",
  "HARDWOOD",
  "SYNTHETIC_COURT",
  "CONCRETE",
  "CLAY",
  "SAND",
  "OTHER",
] as const satisfies readonly VenueSurface[];

export type VenueDuplicateCandidate = Schemas["VenueDuplicateCandidateResponse"];
export type VenueDuplicateEvidence = Schemas["VenueDuplicateEvidence"];

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

/** `fresh`: read again on opening even when a read is kept (Edit details starts from the venue as it is now). */
export function useVenue(venueId: string, fresh = false) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: venueQueryKey(tenant.tenantId, venueId),
    queryFn: () => call(client.GET("/api/admin/v1/venues/{venueId}", { params: { path: { venueId } } })),
    refetchOnMount: fresh ? "always" : true,
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

/**
 * Keeps the venue a change answered and marks the directory, the history and the duplicate checks kept stale: a
 * venue just added or renamed must be found by the next check (#138 R3).
 */
export function useKeepVenue() {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return (venue: Venue) => {
    queryClient.setQueryData(venueQueryKey(tenant.tenantId, venue.id), venue);
    void queryClient.invalidateQueries({ queryKey: ["venues", tenant.tenantId] });
    void queryClient.invalidateQueries({ queryKey: venueStatusChangesQueryKey(tenant.tenantId, venue.id) });
    // Stale, not read now: the next live check or save check asks again.
    void queryClient.invalidateQueries({ queryKey: ["venue-duplicates", tenant.tenantId], refetchType: "none" });
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

/** Whether the user may add a venue: the create and its live check (D13: granted together). */
export const addVenueActions = ["venue.create", "venue.duplicate_check"] as const;

/** Edit details: the update and the live check of a changed name, city or coordinates. */
export const editVenueActions = ["venue.update", "venue.duplicate_check"] as const;

const duplicatesStaleTime = 60 * 1000;
const duplicatesQueryKey = (tenantId: string, key: string) => ["venue-duplicates", tenantId, key] as const;

/**
 * The venues that may be the one typed (D13), asked once `request` is set; `exceptId` leaves the venue being edited
 * out (the check doesn't know it). Kept for a minute: the same input asks again only after that.
 */
export function useVenueDuplicates(request: CheckVenueDuplicatesRequest | null, key: string, exceptId?: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: duplicatesQueryKey(tenant.tenantId, key),
    queryFn: () => call(client.POST("/api/admin/v1/venues/duplicate-check", { body: request! })),
    enabled: request !== null && can("venue.duplicate_check"),
    staleTime: duplicatesStaleTime,
    // The list stays while the next input is checked, so the warning doesn't flicker away and back.
    placeholderData: keepPreviousData,
    select: (answer) => answer.candidates.filter((candidate) => candidate.venueId !== exceptId),
  });
}

/**
 * The check for exactly this input, now (the save's: the live list may still answer earlier typing). Shares the live
 * warning's cache, so an input already checked isn't asked again.
 */
export function useCheckVenueDuplicatesNow(exceptId?: string) {
  const { tenant, client } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return async (request: CheckVenueDuplicatesRequest, key: string): Promise<VenueDuplicateCandidate[]> => {
    const answer = await queryClient.fetchQuery({
      queryKey: duplicatesQueryKey(tenant.tenantId, key),
      queryFn: () => call(client.POST("/api/admin/v1/venues/duplicate-check", { body: request })),
      staleTime: duplicatesStaleTime,
    });
    return answer.candidates.filter((candidate) => candidate.venueId !== exceptId);
  };
}

/** The venues the save's `VENUE_DUPLICATE_REVIEW_REQUIRED` names (its Problem Details' `candidates`), or null. */
export function reviewCandidates(error: unknown): VenueDuplicateCandidate[] | null {
  if (!isApiProblem(error, "VENUE_DUPLICATE_REVIEW_REQUIRED")) {
    return null;
  }
  const candidates = (error.problem as { candidates?: VenueDuplicateCandidate[] | null }).candidates;
  // Without a list there is nothing to review: the refusal shows as it is (#138 N3).
  return candidates?.length ? candidates : null;
}

/** Names & IDs' calls for a venue (`venue.alias.*`, `venue.external_id.*`), each from the version its dialog opened on. */
export function useVenueNamesApi(venueId: string): NamesAndIdsApi<Venue> {
  const { client } = useTenantWorkspace();
  const keep = useKeepVenue();
  const reload = useReloadVenue(venueId);
  const path = (opened: OpenedVenue) => ({ path: { venueId: opened.id }, header: ifMatch(opened.version) });
  return {
    addIdentifier: (opened, body) =>
      call(client.POST("/api/admin/v1/venues/{venueId}/external-identifiers", { params: path(opened), body })),
    endIdentifier: (opened, identifierId, body) =>
      call(
        client.POST("/api/admin/v1/venues/{venueId}/external-identifiers/{identifierId}/end", {
          params: { ...path(opened), path: { venueId: opened.id, identifierId } },
          body,
        }),
      ),
    addAlias: (opened, body) =>
      call(client.POST("/api/admin/v1/venues/{venueId}/aliases", { params: path(opened), body })),
    removeAlias: (opened, aliasId, reason) =>
      call(
        client.POST("/api/admin/v1/venues/{venueId}/aliases/{aliasId}/remove", {
          params: { ...path(opened), path: { venueId: opened.id, aliasId } },
          body: { reason },
        }),
      ),
    keep,
    reload,
  };
}
