"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import { useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { ConflictAlert } from "@/components/conflict-alert";
import { FormField } from "@/components/form-field";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { DuplicatePanel } from "@/components/registry/duplicate-panel";
import { useDuplicateReview } from "@/components/registry/use-duplicate-review";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { BackLink } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { isApiProblem } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { isConcurrencyConflict } from "@/lib/concurrency";
import { formatDay } from "@/lib/entitlements";
import { needsAlert } from "@/lib/form-errors";
import { todayIn, useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "../../../sports/sports-data";
import { applyTeamErrors, toPanelCandidate } from "../../team-editor";
import {
  checkKey,
  limits,
  renameApiFields,
  renameSchema,
  renameStart,
  toRenameCheckRequest,
  toRenameRequest,
  type RenameValues,
} from "../../team-form";
import {
  acceptsRename,
  renameClosedReason,
  renameTeamActions,
  reviewCandidates,
  teamDuplicatesQueryName,
  teamQueryKey,
  useCheckTeamDuplicates,
  useRenameTeam,
  useTeam,
  type Team,
} from "../../teams-data";

/**
 * Registry → Teams → a team → Rename (S07 D8, D13, D23): a new name from a day, with the short name and code from then
 * on (prefilled with the current ones, kept unless changed). The old name becomes a former name with its dates
 * (History), so earlier results keep showing it. The day is after the current name's first day and not after today;
 * a future rename waits for scheduled jobs (Later). New names run the duplicate warning, the team itself left out. A
 * disbanded or archived team isn't renamed (`TEAM_DISBANDED`, `TEAM_ARCHIVED`).
 */
export default function RenameTeamPage() {
  const { teamId } = useParams<{ teamId: string }>();
  const { tenant, can, unavailable } = useTenantWorkspace();
  const { allowed, reason } = actionAccess(renameTeamActions, can, unavailable);
  const queryClient = useQueryClient();
  const team = useTeam(teamId, true);
  // As Edit details: the form starts from the team as read since the page opened; only Reload starts it over.
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
        <h1 className="text-2xl font-semibold">Rename</h1>
        <UnavailableNote id="rename-team-unavailable" reason={reason} />
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
  if (!acceptsRename(team.data.status)) {
    return (
      <section className="grid gap-4">
        {back}
        <h1 className="text-2xl font-semibold">Rename</h1>
        <p className="text-muted-foreground">{renameClosedReason(team.data)}</p>
      </section>
    );
  }
  return <RenameForm team={team.data} teamHref={teamHref} onReload={() => void reload()} />;
}

function RenameForm({ team, teamHref, onReload }: Readonly<{ team: Team; teamHref: string; onReload: () => void }>) {
  const { tenant, can } = useTenantWorkspace();
  const router = useRouter();
  const today = todayIn(useTenantTimeZone());
  const published = useSportVariants();
  // The team as the page opened on it: the rename's basis.
  const [opened] = useState(() => ({ team, values: renameStart(team, today) }));
  const rename = useRenameTeam({ id: team.id, version: opened.team.version });
  const form = useForm<RenameValues, unknown, RenameValues>({
    resolver: zodResolver(renameSchema(opened.team.nameSince, today)),
    defaultValues: opened.values,
    mode: "onTouched",
  });
  const [name, shortName] = useWatch({
    control: form.control,
    name: ["name", "shortName"],
  });
  const sportName = (code: string) =>
    published.data?.items.find((item) => item.variant === code)?.name ?? humanizeCode(code);

  const check = useCheckTeamDuplicates();
  const {
    shown,
    checking,
    panelRef,
    panelMessage,
    decide,
    decision,
    submit: submitReview,
  } = useDuplicateReview(
    { ...opened.values, name, shortName },
    {
      noun: "team",
      queryName: teamDuplicatesQueryName,
      action: "team.duplicate_check",
      requestFor: (values: RenameValues) => toRenameCheckRequest(opened.team, values),
      keyOf: checkKey,
      check,
      idOf: (candidate) => candidate.teamId,
      exceptId: team.id,
      reviewCandidates,
    },
  );

  const submit = form.handleSubmit((values) =>
    submitReview(values, (reviewDecision, refusedForReview) =>
      rename.mutate(toRenameRequest(values, reviewDecision), {
        onSuccess: () => router.push(`${teamHref}?tab=history`),
        onError: (error) => {
          if (refusedForReview(error)) {
            return;
          }
          applyTeamErrors(error, renameApiFields, form.setError);
          // The server's date and name refusals speak to API clients; the form says what to change.
          if (isApiProblem(error, "TEAM_RENAME_DATE_INVALID")) {
            form.setError("effectiveOn", {
              type: "server",
              message: `Choose a day after ${formatDay(opened.team.nameSince)} and no later than today.`,
            });
          }
          if (isApiProblem(error, "TEAM_NAME_UNCHANGED")) {
            form.setError("name", {
              type: "server",
              message: "This is the team's name already. Enter the new name.",
            });
          }
        },
      }),
    ),
  );

  const errors = form.formState.errors;
  const busy = rename.isPending || checking;
  const reviewRefusal = rename.isError && reviewCandidates(rename.error) !== null;
  const count = shown.length === 1 ? "1 team" : `${shown.length} teams`;

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <BackLink href={teamHref}>{team.name}</BackLink>
      <header className="grid gap-1">
        <p className="text-sm text-muted-foreground break-words">{team.name}</p>
        <h1 className="text-2xl font-semibold">Rename</h1>
        <p className="text-muted-foreground">
          {team.name} has had its name since {formatDay(team.nameSince)}. It stays as a former name until the day before
          the new one starts, so earlier results keep showing it. Use Edit details to correct a typing mistake in the
          short name or code instead.
        </p>
      </header>
      <form className="grid gap-5" noValidate onSubmit={(event) => void submit(event)}>
        <p role="status" className="sr-only">
          {shown.length > 0 ? `${count} may be the same as this one. The list is under the new name.` : ""}
        </p>
        {isConcurrencyConflict(rename.error) ? (
          <ConflictAlert onReload={onReload} />
        ) : rename.isError && !reviewRefusal && needsAlert(rename.error, renameApiFields) ? (
          <ProblemAlert error={rename.error} />
        ) : null}
        <fieldset className="grid gap-4 rounded-md border p-4">
          <legend className="px-1 font-semibold">The new name</legend>
          <FormField
            id="rename-name"
            label="New name"
            hint="The name it plays under from the day below."
            autoComplete="off"
            maxLength={limits.name}
            error={errors.name?.message}
            {...form.register("name")}
          />
          <div className="grid gap-4 tablet:grid-cols-2">
            <FormField
              id="rename-short-name"
              label="Short name (optional)"
              hint="From the same day. Empty: no short name."
              autoComplete="off"
              maxLength={limits.shortName}
              error={errors.shortName?.message}
              {...form.register("shortName")}
            />
            <FormField
              id="rename-code"
              label="Code (optional)"
              hint="Three letters, from the same day. Empty: no code."
              autoComplete="off"
              autoCapitalize="characters"
              className="uppercase"
              maxLength={3}
              error={errors.code?.message}
              {...form.register("code")}
            />
          </div>
          <FormField
            id="rename-effective-on"
            label="New name from"
            hint={`After ${formatDay(team.nameSince)}, and no later than today.`}
            type="date"
            min={nextDay(team.nameSince)}
            max={today}
            error={errors.effectiveOn?.message}
            {...form.register("effectiveOn")}
          />
        </fieldset>
        {shown.length > 0 ? (
          <DuplicatePanel
            ref={panelRef}
            noun="team"
            candidates={shown.map((candidate) =>
              toPanelCandidate(candidate, `${tenantPath(tenant.slug)}/teams/${candidate.teamId}`, sportName),
            )}
            decision={decision}
            onDecisionChange={decide}
            canOpen={can("team.read")}
            error={panelMessage}
            disabled={busy}
          />
        ) : null}
        <div className="flex flex-wrap gap-3">
          <Button type="submit" disabled={busy}>
            {checking ? "Checking…" : rename.isPending ? "Renaming…" : "Rename"}
          </Button>
          <Button asChild variant="outline">
            <Link href={teamHref}>Cancel</Link>
          </Button>
        </div>
      </form>
    </section>
  );
}

/** The day after an ISO day (the earliest new name's first day). */
function nextDay(day: string): string {
  const date = new Date(`${day}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}
