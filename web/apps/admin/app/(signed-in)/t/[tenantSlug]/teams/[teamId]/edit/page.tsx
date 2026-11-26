"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { BackLink } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { call, isApiProblem } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { ifMatch } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "../../../sports/sports-data";
import { TeamEditor, type TeamSave } from "../../team-editor";
import { fromTeam, toUpdateRequest } from "../../team-form";
import {
  acceptsRename,
  editTeamActions,
  renameTeamActions,
  teamQueryKey,
  useKeepTeam,
  useTeam,
  type Team,
} from "../../teams-data";

/**
 * Registry → Teams → a team → Edit details (S07 D6, D7, D11, D23): every detail but the sport and the name, the web
 * address included, in any status (a correction). The `PUT` replaces them all from the version the page opened on
 * (`If-Match`) and sends the current name (D8: another is a rename); a change to the short name, the category or the
 * organisation runs the duplicate warning again. The status, aliases, identifiers and images have their own actions.
 */
export default function EditTeamPage() {
  const { teamId } = useParams<{ teamId: string }>();
  const { tenant, can, unavailable } = useTenantWorkspace();
  const { allowed, reason } = actionAccess(editTeamActions, can, unavailable);
  const queryClient = useQueryClient();
  const team = useTeam(teamId, true);
  // The form starts once the team has been read since the page opened (a cached one may be older than its version)
  // and never resets what is being typed; only Reload starts it over (as the organisation's Edit details).
  const [started, setStarted] = useState(false);
  const [reloading, setReloading] = useState(false);
  if (!started && !reloading && team.isFetchedAfterMount && team.isSuccess && !team.isFetching) {
    setStarted(true);
  }
  const reload = async () => {
    setStarted(false);
    setReloading(true);
    try {
      await queryClient.refetchQueries({
        queryKey: teamQueryKey(tenant.tenantId, teamId),
      });
    } finally {
      setReloading(false);
    }
  };

  if (!can("team.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(team.error, "TEAM_NOT_FOUND")) {
    return <NotFoundView />;
  }
  const teamHref = `${tenantPath(tenant.slug)}/teams/${teamId}`;
  const back = <BackLink href={teamHref}>{team.data?.name ?? "Back"}</BackLink>;
  if (!allowed) {
    return reason ? (
      <section className="grid gap-4">
        {back}
        <h1 className="text-2xl font-semibold">Edit details</h1>
        <UnavailableNote id="edit-team-unavailable" reason={reason} />
      </section>
    ) : (
      <ForbiddenView />
    );
  }
  if (!started || !team.data) {
    const error = reloading ? null : team.error;
    return (
      <section className="grid gap-4">
        {back}
        {error ? <ProblemAlert error={error} /> : <Loading />}
      </section>
    );
  }
  return <EditDetails team={team.data} teamHref={teamHref} onReload={() => void reload()} />;
}

function EditDetails({ team, teamHref, onReload }: Readonly<{ team: Team; teamHref: string; onReload: () => void }>) {
  const { client, can } = useTenantWorkspace();
  const router = useRouter();
  const keep = useKeepTeam();
  const published = useSportVariants();
  // The team as the page opened on it: the change's basis and what the warning compares with.
  const [opened] = useState(() => ({ team, values: fromTeam(team) }));
  const save = useMutation({
    mutationFn: ({ values, decision }: TeamSave) =>
      call(
        client.PUT("/api/admin/v1/teams/{teamId}", {
          params: {
            path: { teamId: team.id },
            header: ifMatch(opened.team.version),
          },
          body: toUpdateRequest(opened.team, values, decision),
        }),
      ),
    onSuccess: (saved) => {
      keep(saved, opened.team.organisation?.id);
      router.push(teamHref);
    },
  });
  const sports = (published.data?.items ?? []).map(({ variant, name }) => ({
    variant,
    name,
  }));
  const sportName = (code: string) => sports.find((sport) => sport.variant === code)?.name ?? humanizeCode(code);
  const renameHref =
    acceptsRename(team.status) && renameTeamActions.every((action) => can(action)) ? `${teamHref}/rename` : null;

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <BackLink href={teamHref}>{team.name}</BackLink>
      <header className="grid gap-1">
        <p className="text-sm text-muted-foreground break-words">{team.name}</p>
        <h1 className="text-2xl font-semibold">Edit details</h1>
        <p className="text-muted-foreground">
          A change to the short name, the category or the organisation is checked against the teams you already have.
          The status, other names, identifiers, logo and badge are changed on the team&apos;s page.
        </p>
      </header>
      <TeamEditor
        initial={opened.values}
        team={team}
        sports={sports}
        sportName={sportName}
        renameHref={renameHref}
        save={save}
        submitLabel="Save"
        pendingLabel="Saving…"
        cancelHref={teamHref}
        onReload={onReload}
      />
    </section>
  );
}
