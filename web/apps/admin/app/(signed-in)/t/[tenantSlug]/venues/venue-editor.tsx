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
  useCheckVenueDuplicatesNow,
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
  const requestFor = (values: VenueValues) =>
    edit && !changesComparedFields(initial, values) ? null : toCheckRequest(values);
  const request = requestFor({ ...initial, name, city, latitude, longitude });
  const key = checkKey(request);
  const [asked, setAsked] = useState<{ key: string; request: typeof request }>({ key: "", request: null });
  useEffect(() => {
    const timer = setTimeout(() => setAsked({ key, request }), checkDelay);
    return () => clearTimeout(timer);
    // `request` follows `key`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  const live = useVenueDuplicates(asked.request, asked.key, venue?.id);
  const checkNow = useCheckVenueDuplicatesNow(venue?.id);
  const [checking, setChecking] = useState(false);

  // The server's list, while the input it answered for is unchanged.
  const [refused, setRefused] = useState<{ key: string; candidates: VenueDuplicateCandidate[] } | null>(null);
  const shown =
    refused && refused.key === asked.key ? refused.candidates : asked.request ? (live.data ?? []) : [];

  // "This is a different one" holds for the venues it was said about: a new one shows the choice again.
  const [decision, setDecision] = useState<DuplicateDecision>(undecided);
  const [decidedFor, setDecidedFor] = useState<readonly string[]>([]);
  const decidedOn = (list: readonly VenueDuplicateCandidate[]) =>
    decision.reviewed && list.every((candidate) => decidedFor.includes(candidate.venueId));
  const reviewed = decidedOn(shown);
  // A refusal under the panel belongs to the input it was said for: new typing clears it (pre-review 3).
  const [panelError, setPanelError] = useState<{ key: string; message: string } | null>(null);
  const panelMessage = panelError && panelError.key === asked.key ? panelError.message : null;
  const panel = useRef<HTMLElement>(null);
  // Each request moves the focus to the panel once it is shown (a refused save shows it in the same render).
  const [focusRequests, setFocusRequests] = useState(0);
  useEffect(() => {
    if (focusRequests > 0) {
      panel.current?.focus();
    }
  }, [focusRequests]);
  const stop = (forKey: string, message: string) => {
    setPanelError({ key: forKey, message });
    setFocusRequests((count) => count + 1);
  };

  const decide = (next: DuplicateDecision) => {
    setDecision(next);
    setDecidedFor(next.reviewed ? shown.map((candidate) => candidate.venueId) : []);
    setPanelError(null);
  };

  // The save decides on the list for exactly what is saved, never on a list for earlier typing (pre-review 1): a
  // decision goes with the save only when every venue that input matches was decided on.
  const submit = form.handleSubmit(async (values) => {
    const current = requestFor(values);
    const currentKey = checkKey(current);
    let list: VenueDuplicateCandidate[] = [];
    if (refused && refused.key === currentKey) {
      list = refused.candidates;
    } else if (current) {
      setChecking(true);
      try {
        list = await checkNow(current, currentKey);
      } catch {
        // The save goes without a decision: the server checks again and sends back any match.
        list = [];
      } finally {
        setChecking(false);
      }
    }
    setAsked({ key: currentKey, request: current });
    if (list.length > 0 && !decidedOn(list)) {
      stop(currentKey, reviewFirst);
      return;
    }
    setPanelError(null);
    save.mutate(
      { values, decision: list.length > 0 ? { reviewed: true, reason: decision.reason } : undecided },
      {
        onError: (error) => {
          const candidates = reviewCandidates(error);
          if (candidates) {
            setRefused({ key: currentKey, candidates });
            setDecision(undecided);
            setDecidedFor([]);
            stop(currentKey, "Someone added a venue that may match. Review the list before saving.");
            return;
          }
          applyFieldErrors(error, apiFields, form.setError);
        },
      },
    );
  });

  const busy = save.isPending || checking;
  const venueHref = (id: string) => `${tenantPath(tenant.slug)}/venues/${id}`;
  const reviewRefusal = save.isError && reviewCandidates(save.error) !== null;
  const warning =
    shown.length > 0 ? (
      <DuplicatePanel
        ref={panel}
        noun="venue"
        candidates={shown.map((candidate) => toPanelCandidate(candidate, venueHref(candidate.venueId)))}
        // Undecided again (a new venue), the reason typed before is kept for the next decision.
        decision={reviewed ? decision : { reviewed: false, reason: decision.reason }}
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
