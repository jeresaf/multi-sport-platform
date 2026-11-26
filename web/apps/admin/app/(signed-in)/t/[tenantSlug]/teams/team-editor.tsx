"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import type { UseMutationResult } from "@tanstack/react-query";
import Link from "next/link";
import { useForm, useWatch, type FieldValues, type Path, type UseFormSetError } from "react-hook-form";
import { ConflictAlert } from "@/components/conflict-alert";
import { ProblemAlert } from "@/components/problem-alert";
import { DuplicatePanel, type DuplicateDecision, type PanelCandidate } from "@/components/registry/duplicate-panel";
import { useDuplicateReview } from "@/components/registry/use-duplicate-review";
import { isApiProblem } from "@/lib/api";
import { isConcurrencyConflict } from "@/lib/concurrency";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { teamCategoryLabel } from "@/lib/team-category";
import { todayIn, useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { TeamFields, type SportChoice } from "./team-fields";
import {
  changesComparedFields,
  checkKey,
  teamApiFields,
  teamSchema,
  toCheckRequest,
  type TeamValues,
} from "./team-form";
import {
  reviewCandidates,
  teamDuplicatesQueryName,
  useCheckTeamDuplicates,
  type Team,
  type TeamDuplicateCandidate,
  type TeamDuplicateEvidence,
} from "./teams-data";

/** One phrase per piece of evidence: "Same name", "Names 86% alike", "Same organisation". */
export function teamEvidenceText({ code, value }: TeamDuplicateEvidence): string {
  switch (code) {
    case "SAME_NAME_KEY":
      return "Same name";
    case "NAME_SIMILARITY":
      return value ? `Names ${Math.round(Number(value) * 100)}% alike` : "Similar names";
    case "SAME_ORGANISATION":
      return "Same organisation";
  }
}

/** A candidate as the panel shows it: its sport (from its definition), category and organisation. */
export function toPanelCandidate(
  candidate: TeamDuplicateCandidate,
  href: string,
  sportName: (variant: string) => string,
): PanelCandidate {
  return {
    id: candidate.teamId,
    name: candidate.name,
    status: candidate.status,
    confidence: candidate.confidence,
    facts: [
      ["Sport", sportName(candidate.variant)],
      ["Category", teamCategoryLabel(candidate)],
      ["Organisation", candidate.organisation?.displayName ?? "Independent"],
    ],
    evidence: candidate.evidence.map(teamEvidenceText),
    href,
  };
}

/** The form's words for the server's refusals of a reference or a sport (their API messages speak to API clients). */
export const teamFieldRefusals = {
  TEAM_ORGANISATION_UNAVAILABLE: [
    "organisationId",
    "This organisation has been dissolved or archived since you chose it. Choose another, or make the team independent.",
  ],
  TEAM_HOME_VENUE_UNAVAILABLE: [
    "homeVenueId",
    "This venue has been closed or archived since you chose it. Choose another venue, or no home venue.",
  ],
  TEAM_SPORT_NOT_ENABLED: ["variant", "This sport is no longer enabled for your organisation. Choose another sport."],
  TEAM_COLOURS_INVALID: [null, "Enter the colours in order: a secondary needs a primary, a third needs a secondary."],
} as const;

/** Puts the server's field refusals under their fields, in the form's words for the codes it knows. */
export function applyTeamErrors<T extends FieldValues>(
  error: unknown,
  apiFields: Readonly<Record<string, Path<T>>>,
  setError: UseFormSetError<T>,
): void {
  applyFieldErrors(error, apiFields, setError);
  for (const [code, [field, message]] of Object.entries(teamFieldRefusals)) {
    if (!isApiProblem(error, code)) {
      continue;
    }
    // A colour refusal names the colour out of order (`errors[].field`).
    const target = field ?? error.problem.errors?.[0]?.field;
    if (target && Object.hasOwn(apiFields, target)) {
      setError(apiFields[target]!, { type: "server", message });
    }
  }
}

export type TeamSave = { values: TeamValues; decision: DuplicateDecision };

/**
 * Add team's and Edit details' form (S07 D6, D7, D13, D23). The duplicate warning follows the names, the sport, the
 * category and the organisation as they are typed (Edit: only once the short name, the category or the organisation
 * changes, the team itself left out: the server's rule). With teams to review, the save waits for **Open** or **This is
 * a different one**; a save the server sends back for review (`TEAM_DUPLICATE_REVIEW_REQUIRED`) shows its list instead.
 */
export function TeamEditor({
  initial,
  team,
  sports,
  sportName,
  presetOrganisation,
  renameHref,
  save,
  submitLabel,
  pendingLabel,
  cancelHref,
  onReload,
}: Readonly<{
  initial: TeamValues;
  /** The team being edited; none for Add team. */
  team?: Team;
  /** Add: the enabled sports. */
  sports: readonly SportChoice[];
  /** Any variant's name, from its definition (the candidates may be in other sports' teams' lists). */
  sportName: (variant: string) => string;
  /** Add from an organisation's Teams tab: that organisation, chosen to start with. */
  presetOrganisation?: { id: string; name: string } | null;
  renameHref?: string | null;
  save: UseMutationResult<Team, unknown, TeamSave>;
  submitLabel: string;
  pendingLabel: string;
  cancelHref: string;
  /** Edit: reads the team again after a concurrency conflict. */
  onReload?: () => void;
}>) {
  const { tenant, can } = useTenantWorkspace();
  const today = todayIn(useTenantTimeZone());
  const edit = team !== undefined;
  const apiFields = teamApiFields(edit);
  const form = useForm<TeamValues, unknown, TeamValues>({
    resolver: zodResolver(teamSchema(edit, today)),
    defaultValues: initial,
    mode: "onTouched",
  });
  const [variant, name, shortName, genderCategory, ageGroup, level, organisationId] = useWatch({
    control: form.control,
    name: ["variant", "name", "shortName", "genderCategory", "ageGroup", "level", "organisationId"],
  });

  const check = useCheckTeamDuplicates();
  const {
    shown,
    checking,
    panelRef,
    panelMessage,
    decide,
    decision: panelDecision,
    submit: submitReview,
  } = useDuplicateReview(
    {
      ...initial,
      variant,
      name,
      shortName,
      genderCategory,
      ageGroup,
      level,
      organisationId,
    },
    {
      noun: "team",
      queryName: teamDuplicatesQueryName,
      action: "team.duplicate_check",
      requestFor: (values: TeamValues) =>
        edit && !changesComparedFields(initial, values) ? null : toCheckRequest(values),
      keyOf: checkKey,
      check,
      idOf: (candidate) => candidate.teamId,
      exceptId: team?.id,
      reviewCandidates,
    },
  );

  const submit = form.handleSubmit((values) =>
    submitReview(values, (decision, refusedForReview) =>
      save.mutate(
        { values, decision },
        {
          onError: (error) => {
            if (!refusedForReview(error)) {
              applyTeamErrors(error, apiFields, form.setError);
            }
          },
        },
      ),
    ),
  );

  const busy = save.isPending || checking;
  const href = (id: string) => `${tenantPath(tenant.slug)}/teams/${id}`;
  const reviewRefusal = save.isError && reviewCandidates(save.error) !== null;
  const warning =
    shown.length > 0 ? (
      <DuplicatePanel
        ref={panelRef}
        noun="team"
        candidates={shown.map((candidate) => toPanelCandidate(candidate, href(candidate.teamId), sportName))}
        decision={panelDecision}
        onDecisionChange={decide}
        canOpen={can("team.read")}
        error={panelMessage}
        // The save has taken the decision as it was: no change to it until the save answers.
        disabled={busy}
      />
    ) : null;
  const count = shown.length === 1 ? "1 team" : `${shown.length} teams`;

  return (
    <form className="grid gap-5" noValidate onSubmit={(event) => void submit(event)}>
      {/* The warning appears while typing: a screen reader hears it then, not only when the save stops. */}
      <p role="status" className="sr-only">
        {shown.length > 0 ? `${count} may be the same as this one. The list is under "Organisation and home venue".` : ""}
      </p>
      {isConcurrencyConflict(save.error) && onReload ? (
        <ConflictAlert onReload={onReload} />
      ) : save.isError && !reviewRefusal && needsAlert(save.error, apiFields) ? (
        <ProblemAlert error={save.error} />
      ) : null}
      <TeamFields
        form={form}
        sports={sports}
        team={team}
        presetOrganisation={presetOrganisation}
        renameHref={renameHref}
        today={today}
        warning={warning}
      />
      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={busy}>
          {checking ? "Checking…" : save.isPending ? pendingLabel : submitLabel}
        </Button>
        <Button asChild variant="outline">
          <Link href={cancelHref}>Cancel</Link>
        </Button>
      </div>
    </form>
  );
}
