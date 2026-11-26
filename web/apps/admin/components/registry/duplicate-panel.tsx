"use client";

// The lighter duplicate warning of organisations, teams and venues (S07 D13, D23): no check token and no remembered
// decision. The form shows the records that may be the one typed, each with **Open**; **This is a different one** lets
// the save go on with `duplicatesReviewed` and an optional reason the audit trail keeps.
import type { components } from "@msp/api-client";
import { Alert, AlertDescription, AlertTitle } from "@msp/ui/components/alert";
import { Badge } from "@msp/ui/components/badge";
import { Button } from "@msp/ui/components/button";
import { Label } from "@msp/ui/components/label";
import { Textarea } from "@msp/ui/components/textarea";
import { TriangleAlert } from "lucide-react";
import Link from "next/link";
import { forwardRef, useId, type ReactNode } from "react";
import { StatusBadge } from "@/components/status-badge";

/** "a" or "an" before the record's noun ("an organisation"). */
export const article = (noun: string) => (/^[aeiou]/i.test(noun) ? "an" : "a");

/** DuplicateDecision's and the change requests' reason limit on the server. */
export const duplicateReasonMaxLength = 500;

export type DuplicateConfidence = components["schemas"]["DuplicateConfidence"];

/** A candidate as the panel shows it: the area's own facts and evidence, already in words. */
export type PanelCandidate = {
  id: string;
  name: string;
  status: string;
  confidence: DuplicateConfidence;
  facts: ReadonlyArray<readonly [string, ReactNode]>;
  evidence: readonly string[];
  href: string;
};

export type DuplicateDecision = { reviewed: boolean; reason: string };
export const undecided: DuplicateDecision = { reviewed: false, reason: "" };

const confidenceWords: Readonly<Record<DuplicateConfidence, string>> = {
  EXACT_IDENTIFIER: "Same identifier",
  LIKELY: "Likely the same",
  POSSIBLE: "Possibly the same",
};

/**
 * The candidates with **Open** (when the user may read them) and the decision. Focusable (`tabIndex -1`) so a save that
 * the server sent back for review can move the focus here. `noun` names the record ("venue").
 */
export const DuplicatePanel = forwardRef<
  HTMLElement,
  Readonly<{
    noun: string;
    candidates: readonly PanelCandidate[];
    decision: DuplicateDecision;
    onDecisionChange: (decision: DuplicateDecision) => void;
    canOpen: boolean;
    /** Said under the list when the save was refused until the list is reviewed. */
    error?: string | null;
    /** While a save is under way: the decision it took can't change. */
    disabled?: boolean;
  }>
>(function DuplicatePanel({ noun, candidates, decision, onDecisionChange, canOpen, error, disabled = false }, ref) {
  const headingId = useId();
  const reasonId = useId();
  const count = candidates.length === 1 ? `1 ${noun}` : `${candidates.length} ${noun}s`;

  return (
    <section
      ref={ref}
      tabIndex={-1}
      aria-labelledby={headingId}
      className="grid gap-4 rounded-md border border-warning p-4 outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <header className="flex items-start gap-3">
        <TriangleAlert aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-warning" />
        <div className="grid gap-1">
          <h2 id={headingId} className="font-semibold">
            Is this {article(noun)} {noun} you already have?
          </h2>
          <p className="text-sm text-muted-foreground">
            {count} may be the same. Open the existing one if it is, rather than adding it twice.
          </p>
        </div>
      </header>

      <ol className="grid gap-3" aria-label={`${noun.charAt(0).toUpperCase()}${noun.slice(1)}s that may match`}>
        {candidates.map((candidate) => (
          <li key={candidate.id} className="grid gap-2 rounded-md border p-3">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium break-words">{candidate.name}</span>
              <Badge variant={candidate.confidence === "POSSIBLE" ? "outline" : "default"}>
                {confidenceWords[candidate.confidence]}
              </Badge>
              <StatusBadge status={candidate.status} />
            </div>
            <dl className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
              {candidate.facts.map(([term, value]) => (
                <div key={term} className="flex gap-1">
                  <dt className="text-muted-foreground">{term}:</dt>
                  <dd className="break-words">{value}</dd>
                </div>
              ))}
            </dl>
            <ul className="list-disc pl-5 text-sm" aria-label="Why it matches">
              {candidate.evidence.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
            {canOpen ? (
              <Button asChild variant="outline" className="h-auto min-h-11 justify-self-start py-2 whitespace-normal">
                <Link href={candidate.href}>Open {candidate.name}</Link>
              </Button>
            ) : null}
          </li>
        ))}
      </ol>

      {decision.reviewed ? (
        <div className="grid gap-3">
          <Alert>
            <AlertTitle>You said this is a different {noun}</AlertTitle>
            <AlertDescription>The save goes ahead; the audit trail keeps the {noun}s you were shown.</AlertDescription>
          </Alert>
          <div className="grid gap-2">
            <Label htmlFor={reasonId}>Why is it different? (optional)</Label>
            <Textarea
              id={reasonId}
              disabled={disabled}
              value={decision.reason}
              maxLength={duplicateReasonMaxLength}
              aria-describedby={`${reasonId}-hint`}
              onChange={(event) => onDecisionChange({ reviewed: true, reason: event.target.value })}
            />
            <p id={`${reasonId}-hint`} className="text-sm text-muted-foreground">
              Kept in the audit trail with your name. Don&apos;t include personal details.
            </p>
          </div>
          <Button
            type="button"
            variant="outline"
            className="justify-self-start"
            disabled={disabled}
            onClick={() => onDecisionChange(undecided)}
          >
            Undo
          </Button>
        </div>
      ) : (
        <div className="grid gap-2">
          <Button
            type="button"
            variant="outline"
            className="justify-self-start"
            disabled={disabled}
            aria-describedby={error ? `${headingId}-error` : undefined}
            onClick={() => onDecisionChange({ reviewed: true, reason: decision.reason })}
          >
            This is a different one
          </Button>
          {error ? (
            <p id={`${headingId}-error`} role="alert" className="text-sm text-destructive">
              {error}
            </p>
          ) : null}
        </div>
      )}
    </section>
  );
});
