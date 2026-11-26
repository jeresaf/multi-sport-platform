"use client";

// Registry → Organisations data (S07 D19, D23): the directory, an organisation, its status history and parent
// links, the status moves, the parent, Add and Edit's duplicate check, Names & IDs and the logo. Every field an organisation read answers is
// public or internal; none is personal.
import type { components } from "@msp/api-client";
import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { call, isApiProblem } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import type { RecordImageApi } from "@/components/registry/image-control";
import type { NamesAndIdsApi } from "@/components/registry/names-and-ids/names-and-ids-data";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

type Schemas = components["schemas"];
export type OrganisationListItem = Schemas["OrganisationListItem"];
export type Organisation = Schemas["OrganisationResponse"];
export type OrganisationStatus = Schemas["OrganisationStatus"];
export type OrganisationType = Schemas["OrganisationType"];
export type OrganisationParentLink = Schemas["OrganisationParentLinkResponse"];
export type CreateOrganisationRequest = Schemas["CreateOrganisationRequest"];
export type UpdateOrganisationRequest = Schemas["UpdateOrganisationRequest"];
export type CheckOrganisationDuplicatesRequest = Schemas["CheckOrganisationDuplicatesRequest"];
export type OrganisationDuplicateCandidate = Schemas["OrganisationDuplicateCandidateResponse"];
export type OrganisationDuplicateEvidence = Schemas["OrganisationDuplicateEvidence"];

export const organisationStatuses: readonly OrganisationStatus[] = [
  "ACTIVE",
  "INACTIVE",
  "SUSPENDED",
  "DISSOLVED",
  "ARCHIVED",
];

/** D4's organisation types, in the form's and the filter's order. */
export const organisationTypes = [
  "CLUB",
  "SCHOOL",
  "UNIVERSITY",
  "ACADEMY",
  "FEDERATION",
  "ASSOCIATION",
  "COMPANY",
  "COMMUNITY",
  "GOVERNMENT",
  "OTHER",
] as const satisfies readonly OrganisationType[];

/** The directory's search and filters, as the address holds them; a missing one is the API's default. */
export type OrganisationFilters = {
  q?: string;
  type?: OrganisationType;
  status?: OrganisationStatus;
};

/** The filters from the address (`?q=&type=&status=`); an unknown type or status is left out. */
export function organisationFilters(params: URLSearchParams): OrganisationFilters {
  return {
    q: params.get("q")?.trim() || undefined,
    type: organisationTypes.find((type) => type === params.get("type")),
    status: organisationStatuses.find((status) => status === params.get("status")),
  };
}

/** Where an organisation is: its city and country, or the country alone. */
export function organisationPlace(item: Pick<OrganisationListItem, "city" | "country">, countryName: (code: string) => string) {
  return item.city ? `${item.city}, ${countryName(item.country)}` : countryName(item.country);
}

/** None of these reads is asked without `organisation.read` (`enabled`): it would only be refused. */
export function useOrganisations(filters: OrganisationFilters) {
  const { tenant, client, can } = useTenantWorkspace();
  return useInfiniteQuery({
    queryKey: ["organisations", tenant.tenantId, filters],
    queryFn: ({ pageParam }) =>
      call(
        client.GET("/api/admin/v1/organisations", { params: { query: { ...filters, cursor: pageParam ?? undefined } } }),
      ),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.page.hasMore ? last.page.nextCursor : undefined),
    enabled: can("organisation.read"),
  });
}

/**
 * The organisations a search for a parent finds (the parent dialog): the first page for the text, every status but
 * archived. Asked once there is text; the list stays while the next text is asked.
 */
export function useParentCandidates(q: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["organisations", tenant.tenantId, { q, purpose: "parent" }],
    queryFn: () => call(client.GET("/api/admin/v1/organisations", { params: { query: { q } } })),
    enabled: q !== "" && can("organisation.read"),
    placeholderData: keepPreviousData,
  });
}

export function organisationQueryKey(tenantId: string, organisationId: string) {
  return ["organisation", tenantId, organisationId] as const;
}

/** `fresh`: read again on opening even when a read is kept (Edit details starts from the organisation as it is now). */
export function useOrganisation(organisationId: string, fresh = false) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: organisationQueryKey(tenant.tenantId, organisationId),
    queryFn: () =>
      call(client.GET("/api/admin/v1/organisations/{organisationId}", { params: { path: { organisationId } } })),
    refetchOnMount: fresh ? "always" : true,
    enabled: can("organisation.read"),
  });
}

export function statusChangesQueryKey(tenantId: string, organisationId: string) {
  return ["organisation-status-changes", tenantId, organisationId] as const;
}

export function useOrganisationStatusChanges(organisationId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: statusChangesQueryKey(tenant.tenantId, organisationId),
    queryFn: () =>
      call(
        client.GET("/api/admin/v1/organisations/{organisationId}/status-changes", {
          params: { path: { organisationId } },
        }),
      ),
    enabled: can("organisation.read"),
  });
}

export function parentLinksQueryKey(tenantId: string, organisationId: string) {
  return ["organisation-parent-links", tenantId, organisationId] as const;
}

/** The parent history (D5), newest first: every link with its dates and reason. */
export function useOrganisationParentLinks(organisationId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: parentLinksQueryKey(tenant.tenantId, organisationId),
    queryFn: () =>
      call(
        client.GET("/api/admin/v1/organisations/{organisationId}/parent-links", {
          params: { path: { organisationId } },
        }),
      ),
    enabled: can("organisation.read"),
  });
}

/**
 * The statuses an organisation may move to from its own (state-machines.md, Organisation): `ACTIVE`, `INACTIVE` and
 * `SUSPENDED` to each other, any of them → `DISSOLVED` (final but for archiving), any but `ARCHIVED` → `ARCHIVED`.
 * Unarchiving goes back to the status before archiving, which only the history knows (`unarchiveTo`).
 */
export function organisationMovesFrom(status: OrganisationStatus): OrganisationStatus[] {
  switch (status) {
    case "ACTIVE":
      return ["INACTIVE", "SUSPENDED", "DISSOLVED", "ARCHIVED"];
    case "INACTIVE":
      return ["ACTIVE", "SUSPENDED", "DISSOLVED", "ARCHIVED"];
    case "SUSPENDED":
      return ["ACTIVE", "INACTIVE", "DISSOLVED", "ARCHIVED"];
    case "DISSOLVED":
      return ["ARCHIVED"];
    case "ARCHIVED":
      return [];
  }
}

type StatusChange = Schemas["OrganisationStatusChangeResponse"];

/** The status an archived organisation goes back to: the one it left for its latest archiving (newest first). */
export function unarchiveTo(changes: readonly StatusChange[]): OrganisationStatus | null {
  const archived = changes.find((change) => change.toStatus === "ARCHIVED");
  return archived?.fromStatus ?? null;
}

/**
 * Whether an organisation may become a parent or take new children, aliases or identifiers: a dissolved or archived
 * one may not (D5, `Organisation.EnsureAcceptsNewChildren`).
 */
export function acceptsNewChildren(status: OrganisationStatus): boolean {
  return status !== "DISSOLVED" && status !== "ARCHIVED";
}

/** The organisation as a dialog first showed it: what a change is based on. */
export type OpenedOrganisation = Pick<Organisation, "id" | "version">;

/**
 * Keeps the organisation a change answered and marks the directory and the histories stale. A new parent's version and
 * children change too (T2c-1), so every organisation read kept is marked stale, not only this one.
 */
export function useKeepOrganisation() {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return (organisation: Organisation) => {
    queryClient.setQueryData(organisationQueryKey(tenant.tenantId, organisation.id), organisation);
    void queryClient.invalidateQueries({ queryKey: ["organisations", tenant.tenantId] });
    void queryClient.invalidateQueries({
      queryKey: ["organisation", tenant.tenantId],
      predicate: (query) => query.queryKey[2] !== organisation.id,
    });
    void queryClient.invalidateQueries({ queryKey: statusChangesQueryKey(tenant.tenantId, organisation.id) });
    void queryClient.invalidateQueries({ queryKey: parentLinksQueryKey(tenant.tenantId, organisation.id) });
    // A kept duplicate check may no longer answer as it did (a name changed): asked again when next needed.
    void queryClient.invalidateQueries({
      queryKey: [organisationDuplicatesQueryName, tenant.tenantId],
      refetchType: "none",
    });
  };
}

/** Reads the organisation again (after a concurrency conflict). */
export function useReloadOrganisation(organisationId: string) {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: organisationQueryKey(tenant.tenantId, organisationId) });
}

/** Moves the status from the version the dialog opened on (`organisation.status.change`, a reason required). */
export function useChangeOrganisationStatus(organisation: OpenedOrganisation) {
  const { client } = useTenantWorkspace();
  const keep = useKeepOrganisation();
  return useMutation({
    mutationFn: ({ status, reason }: { status: OrganisationStatus; reason: string }) =>
      call(
        client.POST("/api/admin/v1/organisations/{organisationId}/status", {
          params: { path: { organisationId: organisation.id }, header: ifMatch(organisation.version) },
          body: { status, reason },
        }),
      ),
    onSuccess: keep,
  });
}

/** Sets or clears the parent from the version the dialog opened on (`organisation.parent.set`, a reason required). */
export function useSetOrganisationParent(organisation: OpenedOrganisation) {
  const { client } = useTenantWorkspace();
  const keep = useKeepOrganisation();
  return useMutation({
    mutationFn: ({ parentId, reason }: { parentId: string | null; reason: string }) =>
      call(
        client.POST("/api/admin/v1/organisations/{organisationId}/parent", {
          params: { path: { organisationId: organisation.id }, header: ifMatch(organisation.version) },
          body: { parentId, reason },
        }),
      ),
    onSuccess: keep,
  });
}

/** Whether the user may add an organisation: the create and its live check (D13: granted together). */
export const addOrganisationActions = ["organisation.create", "organisation.duplicate_check"] as const;

/** Edit details: the update and the live check of a changed name, type or city. */
export const editOrganisationActions = ["organisation.update", "organisation.duplicate_check"] as const;

/** The live warning's cache (`useDuplicateReview`): a save marks it stale. */
export const organisationDuplicatesQueryName = "organisation-duplicates";

/** The organisations that may be the one typed (D13): the check's answer, strongest first. */
export function useCheckOrganisationDuplicates() {
  const { client } = useTenantWorkspace();
  return async (request: CheckOrganisationDuplicatesRequest): Promise<OrganisationDuplicateCandidate[]> =>
    (await call(client.POST("/api/admin/v1/organisations/duplicate-check", { body: request }))).candidates;
}

/** The organisations the save's `ORGANISATION_DUPLICATE_REVIEW_REQUIRED` names (its `candidates`), or null. */
export function reviewCandidates(error: unknown): OrganisationDuplicateCandidate[] | null {
  if (!isApiProblem(error, "ORGANISATION_DUPLICATE_REVIEW_REQUIRED")) {
    return null;
  }
  const candidates = (error.problem as { candidates?: OrganisationDuplicateCandidate[] | null }).candidates;
  // Without a list there is nothing to review: the refusal shows as it is.
  return candidates?.length ? candidates : null;
}

/**
 * The venues a search for the primary venue finds (the form's venue field): the first page for the text, archived ones
 * left out (the venue directory's default). Asked once there is text and only with `venue.read`.
 */
export function useVenueChoices(q: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["venues", tenant.tenantId, { q, purpose: "primary-venue" }],
    queryFn: () => call(client.GET("/api/admin/v1/venues", { params: { query: { q } } })),
    enabled: q !== "" && can("venue.read"),
    placeholderData: keepPreviousData,
  });
}

/** Names & IDs' calls (D10): each from the version its dialog opened on, answering the organisation as changed. */
export function useOrganisationNamesApi(organisationId: string): NamesAndIdsApi<Organisation> {
  const { client } = useTenantWorkspace();
  const keep = useKeepOrganisation();
  const reload = useReloadOrganisation(organisationId);
  const params = (opened: OpenedOrganisation) => ({
    path: { organisationId: opened.id },
    header: ifMatch(opened.version),
  });
  return {
    addIdentifier: (opened, body) =>
      call(
        client.POST("/api/admin/v1/organisations/{organisationId}/external-identifiers", {
          params: params(opened),
          body,
        }),
      ),
    endIdentifier: (opened, identifierId, body) =>
      call(
        client.POST("/api/admin/v1/organisations/{organisationId}/external-identifiers/{identifierId}/end", {
          params: { ...params(opened), path: { organisationId: opened.id, identifierId } },
          body,
        }),
      ),
    addAlias: (opened, body) =>
      call(client.POST("/api/admin/v1/organisations/{organisationId}/aliases", { params: params(opened), body })),
    removeAlias: (opened, aliasId, reason) =>
      call(
        client.POST("/api/admin/v1/organisations/{organisationId}/aliases/{aliasId}/remove", {
          params: { ...params(opened), path: { organisationId: opened.id, aliasId } },
          body: { reason },
        }),
      ),
    keep,
    reload,
  };
}

/**
 * The logo's calls (D15, T4b): set is multipart with one file in `file`, remove a `DELETE`; both from the version the
 * dialog opened on.
 */
export function useOrganisationLogoApi(organisationId: string): RecordImageApi<Organisation> {
  const { client } = useTenantWorkspace();
  const keep = useKeepOrganisation();
  const reload = useReloadOrganisation(organisationId);
  const params = (opened: OpenedOrganisation) => ({
    path: { organisationId: opened.id },
    header: ifMatch(opened.version),
  });
  return {
    set: (opened, file) => {
      const form = new FormData();
      form.append("file", file);
      return call(
        client.PUT("/api/admin/v1/organisations/{organisationId}/logo", {
          params: params(opened),
          body: {},
          // FormData goes as it is: the browser sets the multipart Content-Type and its boundary.
          bodySerializer: () => form,
        }),
      );
    },
    remove: (opened) =>
      call(client.DELETE("/api/admin/v1/organisations/{organisationId}/logo", { params: params(opened) })),
    keep,
    reload,
  };
}
