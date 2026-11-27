"use client";

// Registry → Teams data (S07 D19, D23): the directory, a team, its status history, the status moves, Add, Edit details
// and Rename with their duplicate check, and the organisation and venue searches the form chooses from. Every field a
// team read answers is public or internal; none is personal. Names & IDs (aliases and external identifiers) and the
// logo and badge (T6c-2b) change the team as children of it.
import type { components } from "@msp/api-client";
import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { RecordImageApi } from "@/components/registry/image-control";
import type { NamesAndIdsApi } from "@/components/registry/names-and-ids/names-and-ids-data";
import { call, isApiProblem } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
import { formatDay } from "@/lib/entitlements";
import {
  ageGroups,
  genderCategories,
  teamLevels,
  type AgeGroup,
  type GenderCategory,
  type TeamLevel,
} from "@/lib/team-category";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { organisationQueryKey } from "../organisations/organisations-data";

type Schemas = components["schemas"];
export type TeamListItem = Schemas["TeamListItem"];
export type Team = Schemas["TeamResponse"];
export type TeamStatus = Schemas["TeamStatus"];
export type TeamFormerName = Schemas["TeamFormerNameResponse"];
type StatusChange = Schemas["TeamStatusChangeResponse"];
export type CreateTeamRequest = Schemas["CreateTeamRequest"];
export type UpdateTeamRequest = Schemas["UpdateTeamRequest"];
export type RenameTeamRequest = Schemas["RenameTeamRequest"];
export type CheckTeamDuplicatesRequest = Schemas["CheckTeamDuplicatesRequest"];
export type TeamDuplicateCandidate = Schemas["TeamDuplicateCandidateResponse"];
export type TeamDuplicateEvidence = Schemas["TeamDuplicateEvidence"];

export const teamStatuses: readonly TeamStatus[] = ["ACTIVE", "INACTIVE", "SUSPENDED", "DISBANDED", "ARCHIVED"];

/** The directory's search and filters, as the address holds them; a missing one is the API's default. */
export type TeamFilters = {
  q?: string;
  variant?: string;
  genderCategory?: GenderCategory;
  ageGroup?: AgeGroup;
  level?: TeamLevel;
  status?: TeamStatus;
};

/**
 * The filters from the address (`?q=&variant=&genderCategory=&ageGroup=&level=&status=`); an unknown category or
 * status is left out. The sport is kept as it is: the API answers no team for a variant it doesn't know.
 */
export function teamFilters(params: URLSearchParams): TeamFilters {
  return {
    q: params.get("q")?.trim() || undefined,
    variant: params.get("variant")?.trim() || undefined,
    genderCategory: genderCategories.find((code) => code === params.get("genderCategory")),
    ageGroup: ageGroups.find((code) => code === params.get("ageGroup")),
    level: teamLevels.find((code) => code === params.get("level")),
    status: teamStatuses.find((status) => status === params.get("status")),
  };
}

/** None of these reads is asked without `team.read` (`enabled`): it would only be refused. */
export function useTeams(filters: TeamFilters) {
  const { tenant, client, can } = useTenantWorkspace();
  return useInfiniteQuery({
    queryKey: ["teams", tenant.tenantId, filters],
    queryFn: ({ pageParam }) =>
      call(client.GET("/api/admin/v1/teams", { params: { query: { ...filters, cursor: pageParam ?? undefined } } })),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.page.hasMore ? last.page.nextCursor : undefined),
    enabled: can("team.read"),
  });
}

export function teamQueryKey(tenantId: string, teamId: string) {
  return ["team", tenantId, teamId] as const;
}

/** `fresh`: read again on opening even when a read is kept (Edit details and Rename start from the team as it is now). */
export function useTeam(teamId: string, fresh = false) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: teamQueryKey(tenant.tenantId, teamId),
    queryFn: () => call(client.GET("/api/admin/v1/teams/{teamId}", { params: { path: { teamId } } })),
    refetchOnMount: fresh ? "always" : true,
    enabled: can("team.read"),
  });
}

export function teamStatusChangesQueryKey(tenantId: string, teamId: string) {
  return ["team-status-changes", tenantId, teamId] as const;
}

export function useTeamStatusChanges(teamId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: teamStatusChangesQueryKey(tenant.tenantId, teamId),
    queryFn: () => call(client.GET("/api/admin/v1/teams/{teamId}/status-changes", { params: { path: { teamId } } })),
    enabled: can("team.read"),
  });
}

/**
 * The statuses a team may move to from its own (state-machines.md, Team): `ACTIVE`, `INACTIVE` and `SUSPENDED` to each
 * other, any of them → `DISBANDED`, a disbanded team reformed (`ACTIVE`), any but `ARCHIVED` → `ARCHIVED`.
 * Unarchiving goes back to the status before archiving, which only the history knows (`unarchiveTo`).
 */
export function teamMovesFrom(status: TeamStatus): TeamStatus[] {
  switch (status) {
    case "ACTIVE":
      return ["INACTIVE", "SUSPENDED", "DISBANDED", "ARCHIVED"];
    case "INACTIVE":
      return ["ACTIVE", "SUSPENDED", "DISBANDED", "ARCHIVED"];
    case "SUSPENDED":
      return ["ACTIVE", "INACTIVE", "DISBANDED", "ARCHIVED"];
    case "DISBANDED":
      return ["ACTIVE", "ARCHIVED"];
    case "ARCHIVED":
      return [];
  }
}

/** The status an archived team goes back to: the one it left for its latest archiving (newest first). */
export function unarchiveTo(changes: readonly StatusChange[]): TeamStatus | null {
  const archived = changes.find((change) => change.toStatus === "ARCHIVED");
  return archived?.fromStatus ?? null;
}

/**
 * Whether the team takes a new child (D10): an alias, an identifier, a logo or a badge. A disbanded or archived team
 * doesn't (`TEAM_DISBANDED`, `TEAM_ARCHIVED`); removing and ending work in any status.
 */
export function acceptsNewChildren(status: TeamStatus): boolean {
  return status !== "DISBANDED" && status !== "ARCHIVED";
}

/** The team as a dialog first showed it: what a change is based on. */
export type OpenedTeam = Pick<Team, "id" | "version">;

/**
 * Keeps the team a change answered and marks stale the directories that list teams (the team directory, a venue's
 * home teams), the organisation it belongs to and the one it left (their Teams tabs read the organisation), the
 * subscription's team count and the kept duplicate checks.
 */
export function useKeepTeam() {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return (team: Team, leftOrganisationId?: string | null) => {
    queryClient.setQueryData(teamQueryKey(tenant.tenantId, team.id), team);
    void queryClient.invalidateQueries({ queryKey: ["teams", tenant.tenantId] });
    void queryClient.invalidateQueries({ queryKey: teamStatusChangesQueryKey(tenant.tenantId, team.id) });
    // A move can change the `MAX_TEAMS` count the Subscription page shows (D14), as members' and sports' changes do.
    void queryClient.invalidateQueries({ queryKey: ["tenant-subscription", tenant.tenantId] });
    if (team.organisation) {
      void queryClient.invalidateQueries({ queryKey: organisationQueryKey(tenant.tenantId, team.organisation.id) });
    }
    if (leftOrganisationId && leftOrganisationId !== team.organisation?.id) {
      void queryClient.invalidateQueries({ queryKey: organisationQueryKey(tenant.tenantId, leftOrganisationId) });
    }
    // A kept duplicate check may no longer answer as it did (a name or category changed): asked again when next needed.
    void queryClient.invalidateQueries({ queryKey: [teamDuplicatesQueryName, tenant.tenantId], refetchType: "none" });
  };
}

/** Reads the team again (after a concurrency conflict). */
export function useReloadTeam(teamId: string) {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: teamQueryKey(tenant.tenantId, teamId) });
}

/**
 * Moves the status from the version the dialog opened on (`team.status.change`, a reason required). Reforming and
 * unarchiving count toward `MAX_TEAMS` (D14): at the limit the answer is `ENTITLEMENT_QUOTA_EXCEEDED` with the plan's
 * limit in its detail.
 */
export function useChangeTeamStatus(team: OpenedTeam) {
  const { client } = useTenantWorkspace();
  const keep = useKeepTeam();
  return useMutation({
    mutationFn: ({ status, reason }: { status: TeamStatus; reason: string }) =>
      call(
        client.POST("/api/admin/v1/teams/{teamId}/status", {
          params: { path: { teamId: team.id }, header: ifMatch(team.version) },
          body: { status, reason },
        }),
      ),
    onSuccess: (changed) => keep(changed),
  });
}

/** Whether the user may add a team: the create and its live check (D13: granted together). */
export const addTeamActions = ["team.create", "team.duplicate_check"] as const;

/** Edit details: the update and the live check of a changed short name, category or organisation. */
export const editTeamActions = ["team.update", "team.duplicate_check"] as const;

/** Rename: the rename and the live check of the new names. */
export const renameTeamActions = ["team.rename", "team.duplicate_check"] as const;

/** The day after an ISO day ("2026-11-26" → "2026-11-27"). */
export function nextDay(day: string): string {
  const date = new Date(`${day}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}

/**
 * Why a team can't be renamed today (the tenant's date), or null: not while disbanded or archived (`TEAM_DISBANDED`,
 * `TEAM_ARCHIVED`), and not on the day its name began, as the new name's first day must come after it (D8).
 */
export function renameRefusal(team: Pick<Team, "name" | "status" | "nameSince">, today: string): string | null {
  if (team.status === "ARCHIVED") {
    return `${team.name} is archived: unarchive it before renaming it.`;
  }
  if (team.status === "DISBANDED") {
    return `${team.name} is disbanded: reform it before renaming it.`;
  }
  if (team.nameSince >= today) {
    return `${team.name} has had its name since ${formatDay(team.nameSince)}: it can be renamed from ${formatDay(nextDay(team.nameSince))}.`;
  }
  return null;
}

/** The live warning's cache (`useDuplicateReview`): a save marks it stale. */
export const teamDuplicatesQueryName = "team-duplicates";

/** The teams that may be the one typed (D13): the check's answer, strongest first. */
export function useCheckTeamDuplicates() {
  const { client } = useTenantWorkspace();
  return async (request: CheckTeamDuplicatesRequest): Promise<TeamDuplicateCandidate[]> =>
    (await call(client.POST("/api/admin/v1/teams/duplicate-check", { body: request }))).candidates;
}

/** The teams the save's `TEAM_DUPLICATE_REVIEW_REQUIRED` names (its `candidates`), or null. */
export function reviewCandidates(error: unknown): TeamDuplicateCandidate[] | null {
  if (!isApiProblem(error, "TEAM_DUPLICATE_REVIEW_REQUIRED")) {
    return null;
  }
  const candidates = (error.problem as { candidates?: TeamDuplicateCandidate[] | null }).candidates;
  // Without a list there is nothing to review: the refusal shows as it is.
  return candidates?.length ? candidates : null;
}

/**
 * The organisations a search for the team's organisation finds: the first page for the text, archived ones left out
 * (the directory's default). Asked once there is text and only with `organisation.read`.
 */
export function useOrganisationChoices(q: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["organisations", tenant.tenantId, { q, purpose: "team-organisation" }],
    queryFn: () => call(client.GET("/api/admin/v1/organisations", { params: { query: { q } } })),
    enabled: q !== "" && can("organisation.read"),
    placeholderData: keepPreviousData,
  });
}

/** Rename (D8) from the version the page opened on; the old name becomes a former name (History). */
export function useRenameTeam(team: OpenedTeam) {
  const { client } = useTenantWorkspace();
  const keep = useKeepTeam();
  return useMutation({
    mutationFn: (body: RenameTeamRequest) =>
      call(
        client.POST("/api/admin/v1/teams/{teamId}/rename", {
          params: { path: { teamId: team.id }, header: ifMatch(team.version) },
          body,
        }),
      ),
    onSuccess: (renamed) => keep(renamed),
  });
}

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Add team's organisation to start with (`?organisationId=`, from the organisation's Teams tab): read when the ID is
 * well formed and the user may read organisations. `wanted` says whether there is one to wait for.
 */
export function usePresetOrganisation(organisationId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  const wanted = uuid.test(organisationId) && can("organisation.read");
  const query = useQuery({
    queryKey: organisationQueryKey(tenant.tenantId, organisationId),
    queryFn: () =>
      call(client.GET("/api/admin/v1/organisations/{organisationId}", { params: { path: { organisationId } } })),
    enabled: wanted,
  });
  return { ...query, wanted };
}

/**
 * Names & IDs' calls (D10, T3c, T3d): nicknames and spellings are added and removed (a former name only comes from a
 * rename), identifiers added and ended; each from the version the dialog opened on.
 */
export function useTeamNamesApi(teamId: string): NamesAndIdsApi<Team> {
  const { client } = useTenantWorkspace();
  const keep = useKeepTeam();
  const reload = useReloadTeam(teamId);
  const params = (opened: OpenedTeam) => ({ path: { teamId: opened.id }, header: ifMatch(opened.version) });
  return {
    addIdentifier: (opened, body) =>
      call(client.POST("/api/admin/v1/teams/{teamId}/external-identifiers", { params: params(opened), body })),
    endIdentifier: (opened, identifierId, body) =>
      call(
        client.POST("/api/admin/v1/teams/{teamId}/external-identifiers/{identifierId}/end", {
          params: { ...params(opened), path: { teamId: opened.id, identifierId } },
          body,
        }),
      ),
    addAlias: (opened, body) =>
      call(client.POST("/api/admin/v1/teams/{teamId}/aliases", { params: params(opened), body })),
    removeAlias: (opened, aliasId, reason) =>
      call(
        client.POST("/api/admin/v1/teams/{teamId}/aliases/{aliasId}/remove", {
          params: { ...params(opened), path: { teamId: opened.id, aliasId } },
          body: { reason },
        }),
      ),
    keep: (team) => keep(team),
    reload,
  };
}

/** A team's two images (D15). */
export type TeamImage = "logo" | "badge";

/**
 * The logo's or badge's calls (D15, T4b): set is multipart with one file in `file`, remove a `DELETE`; both from the
 * version the dialog opened on.
 */
export function useTeamImageApi(teamId: string, image: TeamImage): RecordImageApi<Team> {
  const { client } = useTenantWorkspace();
  const keep = useKeepTeam();
  const reload = useReloadTeam(teamId);
  const params = (opened: OpenedTeam) => ({ path: { teamId: opened.id }, header: ifMatch(opened.version) });
  const upload = (file: File) => {
    const form = new FormData();
    form.append("file", file);
    // FormData goes as it is: the browser sets the multipart Content-Type and its boundary.
    return { body: {}, bodySerializer: () => form };
  };
  return {
    set: (opened, file) =>
      image === "logo"
        ? call(client.PUT("/api/admin/v1/teams/{teamId}/logo", { params: params(opened), ...upload(file) }))
        : call(client.PUT("/api/admin/v1/teams/{teamId}/badge", { params: params(opened), ...upload(file) })),
    remove: (opened) =>
      image === "logo"
        ? call(client.DELETE("/api/admin/v1/teams/{teamId}/logo", { params: params(opened) }))
        : call(client.DELETE("/api/admin/v1/teams/{teamId}/badge", { params: params(opened) })),
    keep: (team) => keep(team),
    reload,
  };
}
