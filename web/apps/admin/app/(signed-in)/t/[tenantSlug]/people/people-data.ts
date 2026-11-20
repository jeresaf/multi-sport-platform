"use client";

// Registry → People data (S06 D22): the directory, a person, their private details and their status history. A
// person's photo and consent evidence are signed URLs that expire (D17), so the reads that carry them are read again
// a minute before the first one expires; the private read is never kept (`no-store`, read for each visit).
import type { components } from "@msp/api-client";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

type Schemas = components["schemas"];
export type PersonListItem = Schemas["PersonListItem"];
export type Person = Schemas["PersonResponse"];
export type PersonPrivate = Schemas["PersonPrivateResponse"];
export type PersonStatus = Schemas["PersonStatus"];
export type AgeStatus = Schemas["AgeStatus"];
export type ProfileKind = "ATHLETE" | "STAFF" | "OFFICIAL";

export const personStatuses: readonly PersonStatus[] = ["ACTIVE", "INACTIVE", "DECEASED", "ARCHIVED"];
export const ageStatuses: readonly AgeStatus[] = ["ADULT", "MINOR", "UNKNOWN"];
export const profileKinds: readonly ProfileKind[] = ["ATHLETE", "STAFF", "OFFICIAL"];

/** The directory's search and filters, as the address holds them; a missing one is the API's default. */
export type PeopleFilters = {
  q?: string;
  status?: PersonStatus;
  ageStatus?: AgeStatus;
  profile?: ProfileKind;
  variant?: string;
};

const oneOf = <T extends string>(values: readonly T[], value: string | null): T | undefined =>
  values.find((candidate) => candidate === value);

/** The filters from the address (`?q=&status=&ageStatus=&profile=&variant=`); an unknown value is left out. */
export function peopleFilters(params: URLSearchParams): PeopleFilters {
  return {
    q: params.get("q")?.trim() || undefined,
    status: oneOf(personStatuses, params.get("status")),
    ageStatus: oneOf(ageStatuses, params.get("ageStatus")),
    profile: oneOf(profileKinds, params.get("profile")),
    variant: params.get("variant") || undefined,
  };
}

/** None of these reads is asked without `person.read` (`enabled`): it would only be refused. */
export function usePeople(filters: PeopleFilters) {
  const { tenant, client, can } = useTenantWorkspace();
  return useInfiniteQuery({
    queryKey: ["people", tenant.tenantId, filters],
    queryFn: ({ pageParam }) =>
      call(client.GET("/api/admin/v1/people", { params: { query: { ...filters, cursor: pageParam ?? undefined } } })),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.page.hasMore ? last.page.nextCursor : undefined),
    enabled: can("person.read"),
  });
}

const refreshMargin = 60 * 1000;

/** How long until a minute before the first of these URLs expires, or false when there is none. */
export function untilExpiry(expiries: ReadonlyArray<string | null | undefined>, now = Date.now()): number | false {
  const times = expiries.filter((expiry): expiry is string => Boolean(expiry)).map((expiry) => Date.parse(expiry));
  if (times.length === 0) {
    return false;
  }
  return Math.max(Math.min(...times) - refreshMargin - now, 1000);
}

export function personQueryKey(tenantId: string, personId: string) {
  return ["person", tenantId, personId] as const;
}

/** `fresh`: read again on mount even when cached (Edit details starts from a fresh read, review #113 R1). */
export function usePerson(personId: string, fresh = false) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: personQueryKey(tenant.tenantId, personId),
    queryFn: () => call(client.GET("/api/admin/v1/people/{personId}", { params: { path: { personId } } })),
    refetchInterval: (query) => untilExpiry([query.state.data?.photo?.expiresAt]),
    refetchOnMount: fresh ? "always" : true,
    enabled: can("person.read"),
  });
}

/**
 * The date of birth, the guardians and the evidence: only with `person.read_private`, and each read is audited, so it
 * is asked only for a tab that shows them (`shown`: Overview and Consent) and the evidence's URLs are kept fresh only
 * while Consent is open (`consentOpen`).
 */
export function personPrivateQueryKey(tenantId: string, personId: string) {
  return ["person-private", tenantId, personId] as const;
}

export function usePersonPrivate(personId: string, shown: boolean, consentOpen: boolean, fresh = false) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: personPrivateQueryKey(tenant.tenantId, personId),
    queryFn: () => call(client.GET("/api/admin/v1/people/{personId}/private", { params: { path: { personId } } })),
    enabled: can("person.read_private") && shown,
    // Read once per visit: going to Roles and back keeps it (a re-enabled stale query would be read, and audited,
    // again); leaving the page drops it.
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
    // Edit details reads it once more on opening: a kept read may be older than the person's version (review #113 R1).
    refetchOnMount: fresh ? "always" : true,
    refetchInterval: (query) =>
      consentOpen ? untilExpiry(query.state.data?.consents.map((consent) => consent.evidence?.expiresAt) ?? []) : false,
  });
}

export function personStatusChangesQueryKey(tenantId: string, personId: string) {
  return ["person-status-changes", tenantId, personId] as const;
}

export function usePersonStatusChanges(personId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: personStatusChangesQueryKey(tenant.tenantId, personId),
    queryFn: () =>
      call(client.GET("/api/admin/v1/people/{personId}/status-changes", { params: { path: { personId } } })),
    enabled: can("person.read"),
  });
}

export type CheckDuplicatesRequest = Schemas["CheckDuplicatesRequest"];
export type CheckDuplicatesResponse = Schemas["CheckDuplicatesResponse"];
export type DuplicateCandidate = Schemas["DuplicateCandidateResponse"];
export type CreatePersonRequest = Schemas["CreatePersonRequest"];
export type UpdatePersonRequest = Schemas["UpdatePersonRequest"];

/** How many existing people Add person's first step lists while a name is typed. */
export const nameMatchLimit = 10;

/**
 * The existing people a typed name finds (Add person's first step, S06 T7): the directory's own search, so the same
 * prefixes, similarity and aliases. Asked only with a search of two characters or more.
 */
export function useNameMatches(q: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["people", tenant.tenantId, "name-matches", q],
    queryFn: () => call(client.GET("/api/admin/v1/people", { params: { query: { q, limit: nameMatchLimit } } })),
    enabled: can("person.read") && q.length >= 2,
  });
}
