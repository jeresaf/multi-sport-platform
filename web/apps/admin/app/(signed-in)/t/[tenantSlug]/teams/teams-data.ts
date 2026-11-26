"use client";

// Registry → Teams data (S07 D19, D23): the directory, a team, its status history, the status moves, Add, Edit details
// and Rename with their duplicate check, and the organisation and venue searches the form chooses from. Every field a
// team read answers is public or internal; none is personal. Names & IDs and the images (T6c-2b) come next.
import type { components } from "@msp/api-client";
import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { call, isApiProblem } from "@/lib/api";
import { ifMatch } from "@/lib/concurrency";
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
    queryFn: () =>
      call(client.GET("/api/admin/v1/teams/{teamId}/status-changes", { params: { path: { teamId } } })),
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

/** Whether a team may be renamed: not while disbanded or archived (`TEAM_DISBANDED`, `TEAM_ARCHIVED`). */
export function acceptsRename(status: TeamStatus): boolean {
  return status !== "DISBANDED" && status !== "ARCHIVED";
}

/** Why a team can't be renamed now (`acceptsRename`). */
export function renameClosedReason(team: Pick<Team, "name" | "status">): string {
  return team.status === "ARCHIVED"
    ? `${team.name} is archived: unarchive it before renaming it.`
    : `${team.name} is disbanded: reform it before renaming it.`;
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

/** The venues a search for the team's home venue finds, as the organisation's primary venue's (with `venue.read`). */
export function useHomeVenueChoices(q: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["venues", tenant.tenantId, { q, purpose: "home-venue" }],
    queryFn: () => call(client.GET("/api/admin/v1/venues", { params: { query: { q } } })),
    enabled: q !== "" && can("venue.read"),
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
