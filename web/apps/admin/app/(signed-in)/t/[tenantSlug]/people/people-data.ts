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

export function usePerson(personId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: personQueryKey(tenant.tenantId, personId),
    queryFn: () => call(client.GET("/api/admin/v1/people/{personId}", { params: { path: { personId } } })),
    refetchInterval: (query) => untilExpiry([query.state.data?.photo?.expiresAt]),
    enabled: can("person.read"),
  });
}

/** The date of birth, the guardians and the evidence: only with `person.read_private`, each read audited. */
export function usePersonPrivate(personId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["person-private", tenant.tenantId, personId],
    queryFn: () => call(client.GET("/api/admin/v1/people/{personId}/private", { params: { path: { personId } } })),
    enabled: can("person.read_private"),
    gcTime: 0,
    refetchOnWindowFocus: false,
    refetchInterval: (query) =>
      untilExpiry(query.state.data?.consents.map((consent) => consent.evidence?.expiresAt) ?? []),
  });
}

export function usePersonStatusChanges(personId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["person-status-changes", tenant.tenantId, personId],
    queryFn: () =>
      call(client.GET("/api/admin/v1/people/{personId}/status-changes", { params: { path: { personId } } })),
    enabled: can("person.read"),
  });
}
