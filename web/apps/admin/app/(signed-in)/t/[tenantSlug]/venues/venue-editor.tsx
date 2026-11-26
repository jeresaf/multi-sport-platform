"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import type { UseMutationResult } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { ConflictAlert } from "@/components/conflict-alert";
import { ProblemAlert } from "@/components/problem-alert";
import {
  DuplicatePanel,
  undecided,
  type DuplicateDecision,
  type PanelCandidate,
} from "@/components/registry/duplicate-panel";
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
  useVenueDuplicates,
  type Venue,
  type VenueDuplicateCandidate,
  type VenueDuplicateEvidence,
} from "./venues-data";

/** How long the live warning waits after the last key, as the directory's search does. */
const checkDelay = 300;

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

const reviewFirst = "Open the existing venue, or say this is a different one.";

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
  const typed = { ...initial, name, city, latitude, longitude };
  const request = edit && !changesComparedFields(initial, typed) ? null : toCheckRequest(typed);
  const key = checkKey(request);
  const [asked, setAsked] = useState<{ key: string; request: typeof request }>({ key: "", request: null });
  useEffect(() => {
    const timer = setTimeout(() => setAsked({ key, request }), checkDelay);
    return () => clearTimeout(timer);
    // `request` follows `key`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  const live = useVenueDuplicates(asked.request, asked.key, venue?.id);

  // The server's list, while the input it answered for is unchanged.
  const [refused, setRefused] = useState<{ key: string; candidates: VenueDuplicateCandidate[] } | null>(null);
  const shown =
    refused && refused.key === asked.key ? refused.candidates : asked.request ? (live.data ?? []) : [];

  // "This is a different one" holds for the venues it was said about: a new one shows the choice again.
  const [decision, setDecision] = useState<DuplicateDecision>(undecided);
  const [decidedFor, setDecidedFor] = useState<readonly string[]>([]);
  const reviewed = decision.reviewed && shown.every((candidate) => decidedFor.includes(candidate.venueId));
  const [panelError, setPanelError] = useState<string | null>(null);
  const panel = useRef<HTMLElement>(null);
  // Each request moves the focus to the panel once it is shown (a refused save shows it in the same render).
  const [focusRequests, setFocusRequests] = useState(0);
  useEffect(() => {
    if (focusRequests > 0) {
      panel.current?.focus();
    }
  }, [focusRequests]);

  const decide = (next: DuplicateDecision) => {
    setDecision(next);
    setDecidedFor(next.reviewed ? shown.map((candidate) => candidate.venueId) : []);
    setPanelError(null);
  };

  const submit = form.handleSubmit((values) => {
    if (shown.length > 0 && !reviewed) {
      setPanelError(reviewFirst);
      setFocusRequests((count) => count + 1);
      return;
    }
    setPanelError(null);
    save.mutate(
      { values, decision: shown.length > 0 ? { reviewed: true, reason: decision.reason } : undecided },
      {
        onError: (error) => {
          const candidates = reviewCandidates(error);
          if (candidates) {
            const checkedKey = checkKey(toCheckRequest(values));
            setAsked({ key: checkedKey, request: toCheckRequest(values) });
            setRefused({ key: checkedKey, candidates });
            setDecision(undecided);
            setDecidedFor([]);
            setPanelError("Someone added a venue that may match. Review the list before saving.");
            setFocusRequests((count) => count + 1);
            return;
          }
          applyFieldErrors(error, venueApiFields, form.setError);
        },
      },
    );
  });

  const venueHref = (id: string) => `${tenantPath(tenant.slug)}/venues/${id}`;
  const reviewRefusal = save.isError && reviewCandidates(save.error) !== null;
  const warning =
    shown.length > 0 ? (
      <DuplicatePanel
        ref={panel}
        noun="venue"
        candidates={shown.map((candidate) => toPanelCandidate(candidate, venueHref(candidate.venueId)))}
        decision={reviewed ? decision : undecided}
        onDecisionChange={decide}
        canOpen={can("venue.read")}
        error={panelError}
      />
    ) : null;

  return (
    <form className="grid gap-5" noValidate onSubmit={(event) => void submit(event)}>
      {isConcurrencyConflict(save.error) && onReload ? (
        <ConflictAlert onReload={onReload} />
      ) : save.isError && !reviewRefusal && needsAlert(save.error, venueApiFields) ? (
        <ProblemAlert error={save.error} />
      ) : null}
      <VenueFields form={form} edit={edit} warning={warning} />
      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? pendingLabel : submitLabel}
        </Button>
        <Button asChild variant="outline">
          <Link href={cancelHref}>Cancel</Link>
        </Button>
      </div>
    </form>
  );
}
