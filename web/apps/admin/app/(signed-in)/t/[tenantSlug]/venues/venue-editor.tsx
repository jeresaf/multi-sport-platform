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
import { isConcurrencyConflict } from "@/lib/concurrency";
import { humanizeCode } from "@/lib/codes";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { VenueFields } from "./venue-fields";
import {
  changesComparedFields,
  checkKey,
  toCheckRequest,
  venueApiFields,
  venueSchema,
  type VenueValues,
} from "./venue-form";
import {
  reviewCandidates,
  useCheckVenueDuplicates,
  venueDuplicatesQueryName,
  type Venue,
  type VenueDuplicateCandidate,
  type VenueDuplicateEvidence,
} from "./venues-data";

/** One sentence per piece of evidence: "Same name", "Names 86% alike", "Same city", "About 120 m away". */
export function venueEvidenceText({ code, value }: VenueDuplicateEvidence): string {
  switch (code) {
    case "SAME_NAME_KEY":
      return "Same name";
    case "NAME_SIMILARITY":
      return value ? `Names ${Math.round(Number(value) * 100)}% alike` : "Similar names";
    case "SAME_CITY":
      return "Same city";
    case "NEARBY":
      return value ? `About ${Number(value).toLocaleString("en-GB")} m away` : "Close by";
  }
}

export function toPanelCandidate(candidate: VenueDuplicateCandidate, href: string): PanelCandidate {
  return {
    id: candidate.venueId,
    name: candidate.name,
    status: candidate.status,
    confidence: candidate.confidence,
    facts: [
      ["Type", humanizeCode(candidate.type)],
      ["City", candidate.city],
    ],
    evidence: candidate.evidence.map(venueEvidenceText),
    href,
  };
}

export type VenueSave = { values: VenueValues; decision: DuplicateDecision };

/**
 * Add venue's and Edit details' form (S07 D3, D13, D23). The duplicate warning follows the name, the city and the
 * coordinates as they are typed (Edit: only once one of them changes, the venue itself left out). With venues to
 * review, the save waits for **Open** or **This is a different one**; a save the server sends back for review
 * (`VENUE_DUPLICATE_REVIEW_REQUIRED`: someone added one since) shows its list instead, from the top.
 */
export function VenueEditor({
  initial,
  venue,
  save,
  submitLabel,
  pendingLabel,
  cancelHref,
  onReload,
}: Readonly<{
  initial: VenueValues;
  /** The venue being edited; none for Add venue. */
  venue?: Venue;
  save: UseMutationResult<Venue, unknown, VenueSave>;
  submitLabel: string;
  pendingLabel: string;
  cancelHref: string;
  /** Edit: reads the venue again after a concurrency conflict. */
  onReload?: () => void;
}>) {
  const { tenant, can } = useTenantWorkspace();
  const edit = venue !== undefined;
  const apiFields = venueApiFields(edit);
  const form = useForm<VenueValues, unknown, VenueValues>({
    resolver: zodResolver(venueSchema(edit)),
    defaultValues: initial,
    mode: "onTouched",
  });
  const [name, city, latitude, longitude] = useWatch({
    control: form.control,
    name: ["name", "city", "latitude", "longitude"],
  });

  // The check follows the typing once it pauses; Edit asks only about a changed name, city or coordinates.
  const check = useCheckVenueDuplicates();
  const {
    shown,
    checking,
    panelRef,
    panelMessage,
    decide,
    decision: panelDecision,
    submit: submitReview,
  } = useDuplicateReview(
    { ...initial, name, city, latitude, longitude },
    {
      noun: "venue",
      queryName: venueDuplicatesQueryName,
      action: "venue.duplicate_check",
      requestFor: (values: VenueValues) =>
        edit && !changesComparedFields(initial, values) ? null : toCheckRequest(values),
      keyOf: checkKey,
      check,
      idOf: (candidate) => candidate.venueId,
      exceptId: venue?.id,
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
              applyFieldErrors(error, apiFields, form.setError);
            }
          },
        },
      ),
    ),
  );

  const busy = save.isPending || checking;
  const venueHref = (id: string) => `${tenantPath(tenant.slug)}/venues/${id}`;
  const reviewRefusal = save.isError && reviewCandidates(save.error) !== null;
  const warning =
    shown.length > 0 ? (
      <DuplicatePanel
        ref={panelRef}
        noun="venue"
        candidates={shown.map((candidate) => toPanelCandidate(candidate, venueHref(candidate.venueId)))}
        decision={panelDecision}
        onDecisionChange={decide}
        canOpen={can("venue.read")}
        error={panelMessage}
        // The save has taken the decision as it was: no change to it until the save answers (#138 N4).
        disabled={busy}
      />
    ) : null;
  const count = shown.length === 1 ? "1 venue" : `${shown.length} venues`;

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
      <VenueFields form={form} edit={edit} warning={warning} />
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
