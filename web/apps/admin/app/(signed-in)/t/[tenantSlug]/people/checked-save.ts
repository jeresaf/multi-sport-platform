// A save behind the duplicate check (S06 D6, D8): Add person's create and Edit details' update. The check runs first
// (unless the update changes nothing it compares); with people to review, the save waits for the registrar's reason. A
// check that went stale (someone added a match) or expired before the save runs again once, by itself.
import { isApiProblem } from "@/lib/api";
import type { CheckDuplicatesResponse, DuplicateCandidate, Person } from "./people-data";

export type Review<V> = { values: V; checkToken: string; candidates: DuplicateCandidate[]; notice: string | null };
/** What is saved: the values, the review they passed (with its token) and the reason given there. */
export type Attempt<V> = { values: V; reviewed: Review<V> | null; reason: string | null };
export type Outcome<V> = { kind: "saved"; person: Person } | { kind: "review"; review: Review<V> };

export type CheckedSaveSteps<V> = {
  check: (values: V) => Promise<CheckDuplicatesResponse>;
  save: (values: V, checkToken: string | null, reason: string | null) => Promise<Person>;
  /** False when the values change nothing the check compares (an update): the save goes without a token. */
  needsCheck?: (values: V) => boolean;
};

const staleNotice =
  "Someone added a person who may match while you were reviewing, so the check ran again. Review the list again.";
const checkedAgainNotice = "The check had to run again before the save. Review the list.";
const staleBeforeReviewNotice =
  "Someone added a person who may match just before the save, so the check ran again. Review the list.";
const expiredNotice =
  "The check had to run again (it expires after 30 minutes) and the list has changed. Review it again.";

// The same people with the same confidence: a stronger match than the one reviewed is shown again (pre-review m3).
const sameCandidates = (a: readonly DuplicateCandidate[], b: readonly DuplicateCandidate[]) =>
  a.length === b.length &&
  a.every((candidate) =>
    b.some((other) => other.personId === candidate.personId && other.confidence === candidate.confidence),
  );

export async function checkedSave<V>(
  { values, reviewed, reason }: Attempt<V>,
  { check, save, needsCheck = () => true }: CheckedSaveSteps<V>,
): Promise<Outcome<V>> {
  let checkToken: string | null = null;
  if (reviewed) {
    checkToken = reviewed.checkToken;
  } else if (needsCheck(values)) {
    const checked = await check(values);
    if (checked.candidates.length > 0) {
      return { kind: "review", review: { values, ...checked, notice: null } };
    }
    checkToken = checked.checkToken;
  }

  try {
    return { kind: "saved", person: await save(values, checkToken, reason) };
  } catch (error) {
    const stale = isApiProblem(error, "PERSON_DUPLICATE_CHECK_STALE");
    if (!stale && !isApiProblem(error, "PERSON_DUPLICATE_CHECK_REQUIRED")) {
      throw error;
    }
    // Checked again once: with no one to review, or (an expired token) the same people already reviewed with a
    // reason, the save goes ahead; otherwise the registrar reviews the new list.
    const again = await check(values);
    const confirmed = !stale && reason !== null && sameCandidates(again.candidates, reviewed?.candidates ?? []);
    if (again.candidates.length === 0 || confirmed) {
      return {
        kind: "saved",
        person: await save(values, again.checkToken, again.candidates.length > 0 ? reason : null),
      };
    }
    // The notice says "again" only to a registrar who already saw a review (review #109 R5).
    const notice = reviewed
      ? stale
        ? staleNotice
        : expiredNotice
      : stale
        ? staleBeforeReviewNotice
        : checkedAgainNotice;
    return { kind: "review", review: { values, ...again, notice } };
  }
}
