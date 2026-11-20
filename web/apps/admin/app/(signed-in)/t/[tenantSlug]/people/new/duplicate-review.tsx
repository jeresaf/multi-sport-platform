"use client";

import { Alert, AlertDescription, AlertTitle } from "@msp/ui/components/alert";
import { Badge } from "@msp/ui/components/badge";
import { Button } from "@msp/ui/components/button";
import { Label } from "@msp/ui/components/label";
import { Textarea } from "@msp/ui/components/textarea";
import { Info } from "lucide-react";
import Link from "next/link";
import { useId, useRef, useState, type ReactNode } from "react";
import { StatusBadge } from "@/components/status-badge";
import type { DuplicateCandidate } from "../people-data";
import { confidenceLabels, evidenceText } from "./duplicate-evidence";

/** DuplicateDecision.ReasonMaxLength on the server. */
export const reasonMaxLength = 500;

/** What the registrar typed, as the review shows it next to each candidate. The date of birth is their own input. */
export type NewPersonSummary = { displayName: string; born: string; nationality: string };

function Fact({ term, children }: Readonly<{ term: string; children: ReactNode }>) {
  return (
    <div className="grid gap-0.5">
      <dt className="text-sm text-muted-foreground">{term}</dt>
      <dd className="break-words">{children}</dd>
    </div>
  );
}

function Column({ title, children }: Readonly<{ title: ReactNode; children: ReactNode }>) {
  return (
    <div className="grid content-start gap-3 rounded-md border p-4">
      <h3 className="font-semibold">{title}</h3>
      <dl className="grid gap-3">{children}</dl>
    </div>
  );
}

const modes = {
  add: { column: "New person", intro: "", proceed: "Add as a new person", busy: "Adding…" },
  edit: { column: "With your changes", intro: ", with your changes", proceed: "Save the changes", busy: "Saving…" },
} as const;

/**
 * The duplicate review (S06 D5, D6, D8; admin-app.md): each person the check found, next to the new person, with how sure
 * the check is and why. A candidate shows only what `person.read` may see: the birth year, never a date of birth. A
 * shared identifier blocks the save (open the existing person or change the identifier); otherwise the registrar opens
 * an existing person or adds the new one with a reason, which is kept with a "not the same person" decision per
 * candidate. Edit details shows it for the person as changed (`mode` "edit").
 */
export function DuplicateReview({
  mode = "add",
  newPerson,
  candidates,
  notice,
  personHref,
  variantLabel,
  busy,
  canOpen,
  onProceed,
  onChangeDetails,
}: Readonly<{
  /** Add person's review, or Edit details' (the person with the changes). */
  mode?: keyof typeof modes;
  newPerson: NewPersonSummary;
  candidates: readonly DuplicateCandidate[];
  /** Why the review is shown again (the check ran again before the save). */
  notice: string | null;
  personHref: (personId: string) => string;
  variantLabel: (variant: string) => string;
  busy: boolean;
  /** "Open {name}" needs `person.read`; every role that adds people holds it today. */
  canOpen: boolean;
  onProceed: (reason: string) => void;
  onChangeDetails: () => void;
}>) {
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState<string | null>(null);
  const reasonId = useId();
  const reasonBox = useRef<HTMLTextAreaElement>(null);
  const blocked = candidates.filter((candidate) => candidate.confidence === "EXACT_IDENTIFIER");
  const count = candidates.length === 1 ? "1 person" : `${candidates.length} people`;
  const words = modes[mode];

  const proceed = () => {
    const trimmed = reason.trim();
    const refusal = !trimmed
      ? "Say why this is a different person."
      : trimmed.length > reasonMaxLength
        ? `Keep the reason to ${reasonMaxLength} characters.`
        : null;
    if (refusal) {
      // The box gets the focus and the error is announced (review #109 R2).
      setReasonError(refusal);
      reasonBox.current?.focus();
      return;
    }
    setReasonError(null);
    onProceed(trimmed);
  };

  return (
    <section className="grid gap-5" aria-labelledby="duplicate-review-heading">
      <header className="grid gap-1">
        <h2 id="duplicate-review-heading" className="text-xl font-semibold">
          Is this someone you already have?
        </h2>
        <p className="text-muted-foreground">
          {count} may be {newPerson.displayName}
          {words.intro}. Open the existing record if it is the same person.
        </p>
      </header>
      {notice ? (
        <Alert>
          <Info aria-hidden="true" />
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}

      <ol className="grid gap-4" aria-label="People who may match">
        {candidates.map((candidate) => (
          <li key={candidate.personId} className="grid gap-3">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant={candidate.confidence === "POSSIBLE" ? "outline" : "default"}>
                {confidenceLabels[candidate.confidence]}
              </Badge>
            </div>
            <div className="grid gap-3 tablet:grid-cols-2">
              <Column title={words.column}>
                <Fact term="Name">{newPerson.displayName}</Fact>
                <Fact term="Born">{newPerson.born}</Fact>
                <Fact term="Nationality">{newPerson.nationality}</Fact>
              </Column>
              <Column title="Already registered">
                <Fact term="Name">{candidate.displayName}</Fact>
                <Fact term="Born">{candidate.birthYear ?? "Not recorded"}</Fact>
                <Fact term="Status">
                  <StatusBadge status={candidate.status} />
                </Fact>
                <Fact term="Plays">
                  {candidate.athleteVariants.length > 0
                    ? candidate.athleteVariants.map(variantLabel).join(", ")
                    : "No sport recorded"}
                </Fact>
              </Column>
            </div>
            <div className="grid gap-1">
              <h4 className="text-sm font-medium">Why they match</h4>
              <ul className="list-disc pl-5 text-sm">
                {candidate.evidence.map((evidence) => (
                  <li key={`${evidence.code}-${evidence.value ?? ""}`}>{evidenceText(evidence)}</li>
                ))}
              </ul>
            </div>
            {canOpen ? (
              <Button asChild variant="outline" className="h-auto min-h-11 justify-self-start py-2 whitespace-normal">
                <Link href={personHref(candidate.personId)}>Open {candidate.displayName}</Link>
              </Button>
            ) : null}
          </li>
        ))}
      </ol>

      {blocked.length > 0 ? (
        <div className="grid gap-3">
          <Alert variant="destructive">
            <AlertTitle>This identifier is already in use</AlertTitle>
            <AlertDescription>
              {blocked.map((candidate) => candidate.displayName).join(", ")} already{" "}
              {blocked.length === 1 ? "holds" : "hold"} an identifier you entered. One identifier belongs to one person:
              open the existing record, or change the identifier.
            </AlertDescription>
          </Alert>
          <Button variant="outline" className="justify-self-start" onClick={onChangeDetails}>
            Change the details
          </Button>
        </div>
      ) : (
        <form
          className="grid gap-3 rounded-md border p-4"
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            proceed();
          }}
        >
          <h3 className="font-semibold">Not the same person?</h3>
          <div className="grid gap-2">
            <Label htmlFor={reasonId}>Why is {newPerson.displayName} a different person?</Label>
            <Textarea
              id={reasonId}
              ref={reasonBox}
              value={reason}
              maxLength={reasonMaxLength}
              aria-invalid={reasonError ? true : undefined}
              aria-describedby={`${reasonId}-hint${reasonError ? ` ${reasonId}-error` : ""}`}
              onChange={(event) => setReason(event.target.value)}
            />
            <p id={`${reasonId}-hint`} className="text-sm text-muted-foreground">
              Kept with the record for good, so don&apos;t include personal details. For example: &ldquo;Different
              parents; checked with the club.&rdquo;
            </p>
            {reasonError ? (
              <p id={`${reasonId}-error`} role="alert" className="text-sm text-destructive">
                {reasonError}
              </p>
            ) : null}
          </div>
          <div className="flex flex-wrap gap-3">
            <Button type="submit" disabled={busy}>
              {busy ? words.busy : words.proceed}
            </Button>
            <Button type="button" variant="outline" onClick={onChangeDetails}>
              Change the details
            </Button>
          </div>
        </form>
      )}
    </section>
  );
}
