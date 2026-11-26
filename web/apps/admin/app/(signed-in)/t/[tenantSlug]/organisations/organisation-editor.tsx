"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import type { UseMutationResult } from "@tanstack/react-query";
import Link from "next/link";
import { useForm, useWatch } from "react-hook-form";
import { ConflictAlert } from "@/components/conflict-alert";
import { ProblemAlert } from "@/components/problem-alert";
import { DuplicatePanel, type DuplicateDecision, type PanelCandidate } from "@/components/registry/duplicate-panel";
import { useDuplicateReview } from "@/components/registry/use-duplicate-review";
import { isApiProblem } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { isConcurrencyConflict } from "@/lib/concurrency";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { todayIn, useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { OrganisationFields } from "./organisation-fields";
import {
  changesComparedFields,
  checkKey,
  organisationApiFields,
  organisationSchema,
  toCheckRequest,
  type OrganisationValues,
} from "./organisation-form";
import {
  organisationDuplicatesQueryName,
  reviewCandidates,
  useCheckOrganisationDuplicates,
  type Organisation,
  type OrganisationDuplicateCandidate,
  type OrganisationDuplicateEvidence,
} from "./organisations-data";

/** One phrase per piece of evidence: "Same name", "Names 86% alike", "Same type", "Same city". */
export function organisationEvidenceText({ code, value }: OrganisationDuplicateEvidence): string {
  switch (code) {
    case "SAME_NAME_KEY":
      return "Same name";
    case "NAME_SIMILARITY":
      return value ? `Names ${Math.round(Number(value) * 100)}% alike` : "Similar names";
    case "SAME_TYPE":
      return "Same type";
    case "SAME_CITY":
      return "Same city";
  }
}

function toPanelCandidate(candidate: OrganisationDuplicateCandidate, href: string): PanelCandidate {
  return {
    id: candidate.organisationId,
    name: candidate.displayName,
    status: candidate.status,
    confidence: candidate.confidence,
    facts: [
      ["Type", humanizeCode(candidate.type)],
      ["City", candidate.city ?? "—"],
    ],
    evidence: candidate.evidence.map(organisationEvidenceText),
    href,
  };
}

/** The venue field's words for `ORGANISATION_PRIMARY_VENUE_UNAVAILABLE`. */
export const venueUnavailableMessage =
  "This venue has been closed or archived since you chose it. Choose another venue, or no primary venue.";

export type OrganisationSave = { values: OrganisationValues; decision: DuplicateDecision };

/**
 * Add organisation's and Edit details' form (S07 D4, D13, D23). The duplicate warning follows the names, the type and
 * the city as they are typed (Edit: only once one of them changes, the organisation itself left out; owner, T6b plan).
 * With organisations to review, the save waits for **Open** or **This is a different one**; a save the server sends
 * back for review (`ORGANISATION_DUPLICATE_REVIEW_REQUIRED`) shows its list instead.
 */
export function OrganisationEditor({
  initial,
  organisation,
  save,
  submitLabel,
  pendingLabel,
  cancelHref,
  onReload,
}: Readonly<{
  initial: OrganisationValues;
  /** The organisation being edited; none for Add organisation. */
  organisation?: Organisation;
  save: UseMutationResult<Organisation, unknown, OrganisationSave>;
  submitLabel: string;
  pendingLabel: string;
  cancelHref: string;
  /** Edit: reads the organisation again after a concurrency conflict. */
  onReload?: () => void;
}>) {
  const { tenant, can } = useTenantWorkspace();
  const thisYear = Number(todayIn(useTenantTimeZone()).slice(0, 4));
  const edit = organisation !== undefined;
  const apiFields = organisationApiFields(edit);
  const form = useForm<OrganisationValues, unknown, OrganisationValues>({
    resolver: zodResolver(organisationSchema(edit, thisYear)),
    defaultValues: initial,
    mode: "onTouched",
  });
  const [displayName, legalName, shortName, type, city] = useWatch({
    control: form.control,
    name: ["displayName", "legalName", "shortName", "type", "city"],
  });

  const check = useCheckOrganisationDuplicates();
  const {
    shown,
    checking,
    panelRef,
    panelMessage,
    decide,
    decision: panelDecision,
    submit: submitReview,
  } = useDuplicateReview(
    { ...initial, displayName, legalName, shortName, type, city },
    {
      noun: "organisation",
      queryName: organisationDuplicatesQueryName,
      action: "organisation.duplicate_check",
      requestFor: (values: OrganisationValues) =>
        edit && !changesComparedFields(initial, values) ? null : toCheckRequest(values),
      keyOf: checkKey,
      check,
      idOf: (candidate) => candidate.organisationId,
      exceptId: organisation?.id,
      reviewCandidates,
    },
  );

  const submit = form.handleSubmit((values) =>
    submitReview(values, (decision, refusedForReview) =>
      save.mutate(
        { values, decision },
        {
          onError: (error) => {
            if (refusedForReview(error)) {
              return;
            }
            applyFieldErrors(error, apiFields, form.setError);
            // A venue closed or archived since it was found is refused on `primaryVenueId`. The API's message speaks
            // to API clients ("a venue of the tenant"); the form says what happened and what to do (#140 N5).
            if (isApiProblem(error, "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE")) {
              form.setError("primaryVenueId", { type: "server", message: venueUnavailableMessage });
            }
          },
        },
      ),
    ),
  );

  const busy = save.isPending || checking;
  const href = (id: string) => `${tenantPath(tenant.slug)}/organisations/${id}`;
  const reviewRefusal = save.isError && reviewCandidates(save.error) !== null;
  const warning =
    shown.length > 0 ? (
      <DuplicatePanel
        ref={panelRef}
        noun="organisation"
        candidates={shown.map((candidate) => toPanelCandidate(candidate, href(candidate.organisationId)))}
        decision={panelDecision}
        onDecisionChange={decide}
        canOpen={can("organisation.read")}
        error={panelMessage}
        // The save has taken the decision as it was: no change to it until the save answers.
        disabled={busy}
      />
    ) : null;
  const count = shown.length === 1 ? "1 organisation" : `${shown.length} organisations`;

  return (
    <form className="grid gap-5" noValidate onSubmit={(event) => void submit(event)}>
      {/* The warning appears while typing: a screen reader hears it then, not only when the save stops. */}
      <p role="status" className="sr-only">
        {shown.length > 0 ? `${count} may be the same as this one. The list is under "Where it is".` : ""}
      </p>
      {isConcurrencyConflict(save.error) && onReload ? (
        <ConflictAlert onReload={onReload} />
      ) : save.isError && !reviewRefusal && needsAlert(save.error, apiFields) ? (
        <ProblemAlert error={save.error} />
      ) : null}
      <OrganisationFields
        form={form}
        edit={edit}
        savedVenue={organisation?.primaryVenue ?? null}
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
