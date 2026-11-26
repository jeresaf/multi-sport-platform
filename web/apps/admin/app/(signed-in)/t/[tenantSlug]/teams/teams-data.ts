"use client";

// Registry → Teams data (S07 D19, D23): the directory, a team, its status history and the status moves. Every field a
// team read answers is public or internal; none is personal. Add, Edit, Rename (T6c-2a), Names & IDs and the images
// (T6c-2b) come next.
import type { components } from "@msp/api-client";
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { call } from "@/lib/api";
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

export function useTeam(teamId: string) {
  const { tenant, client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: teamQueryKey(tenant.tenantId, teamId),
    queryFn: () => call(client.GET("/api/admin/v1/teams/{teamId}", { params: { path: { teamId } } })),
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
 * Keeps the team a change answered and marks the directories that list teams stale (the team directory, a venue's
 * home teams) and the organisation it belongs to (its Teams tab reads the organisation).
 */
export function useKeepTeam() {
  const { tenant } = useTenantWorkspace();
  const queryClient = useQueryClient();
  return (team: Team) => {
    queryClient.setQueryData(teamQueryKey(tenant.tenantId, team.id), team);
    void queryClient.invalidateQueries({ queryKey: ["teams", tenant.tenantId] });
    void queryClient.invalidateQueries({ queryKey: teamStatusChangesQueryKey(tenant.tenantId, team.id) });
    if (team.organisation) {
      void queryClient.invalidateQueries({ queryKey: organisationQueryKey(tenant.tenantId, team.organisation.id) });
    }
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
    onSuccess: keep,
  });
}
