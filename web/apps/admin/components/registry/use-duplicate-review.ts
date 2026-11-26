"use client";

// The live duplicate warning's state for the registry's Add and Edit forms (S07 D13, D23): venues, organisations and
// teams. The check follows the typing once it pauses and keeps the last list while the next is asked; "This is a
// different one" holds for the records it was said about; the save checks exactly what it saves first, and a save the
// server sends back for review shows the server's list. The area supplies its request, its call and its candidates' IDs.
import { keepPreviousData, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { article, undecided, type DuplicateDecision } from "@/components/registry/duplicate-panel";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

/** How long the live warning waits after the last key, as the directories' search does. */
export const duplicateCheckDelay = 300;
const staleTime = 60 * 1000;

const reviewFirstDefault = (noun: string) => `Open the existing ${noun}, or say this is a different one.`;

export type DuplicateReviewOptions<TValues, TRequest, TCandidate> = Readonly<{
  /** The record's noun ("venue"), for the messages. */
  noun: string;
  /** The cache's name (`venue-duplicates`): a save marks these stale (the area's keep). */
  queryName: string;
  /** The check's action: without it nothing is asked live (the save still checks on the server). */
  action: string;
  /** The request for the values typed (null: nothing to check, as when an edit changes no compared field). */
  requestFor: (values: TValues) => TRequest | null;
  /** The request as one key: the warning asks again only when it changes. */
  keyOf: (request: TRequest | null) => string;
  /** The check's call: the candidates of one request. */
  check: (request: TRequest) => Promise<readonly TCandidate[]>;
  idOf: (candidate: TCandidate) => string;
  /** The record being edited, left out of the answer (the check doesn't know it). */
  exceptId?: string;
  /** The save's `…_DUPLICATE_REVIEW_REQUIRED` candidates, or null for any other failure. */
  reviewCandidates: (error: unknown) => TCandidate[] | null;
}>;

/**
 * `watched` is the form's current values (what the live warning follows). `submit` runs the save: `send` gets the
 * decision to send and `refusedForReview`, which takes a failure and says whether it was the server's review list (then shown).
 */
export function useDuplicateReview<TValues, TRequest, TCandidate>(
  watched: TValues,
  options: DuplicateReviewOptions<TValues, TRequest, TCandidate>,
) {
  const { noun, queryName, action, requestFor, keyOf, check, idOf, exceptId, reviewCandidates } = options;
  const { tenant, can } = useTenantWorkspace();
  const queryClient = useQueryClient();
  const others = (list: readonly TCandidate[]) => list.filter((candidate) => idOf(candidate) !== exceptId);

  const request = requestFor(watched);
  const key = keyOf(request);
  const [asked, setAsked] = useState<{ key: string; request: TRequest | null }>({ key: "", request: null });
  useEffect(() => {
    const timer = setTimeout(() => setAsked({ key, request }), duplicateCheckDelay);
    return () => clearTimeout(timer);
    // `request` follows `key`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  const queryKey = (forKey: string) => [queryName, tenant.tenantId, forKey] as const;
  const live = useQuery({
    queryKey: queryKey(asked.key),
    queryFn: () => check(asked.request!),
    enabled: asked.request !== null && can(action),
    staleTime,
    // The list stays while the next input is checked, so the warning doesn't flicker away and back.
    placeholderData: keepPreviousData,
  });
  // The check for exactly this input, now (the save's: the live list may still answer earlier typing). Shares the
  // live warning's cache, so an input already checked isn't asked again.
  const checkNow = async (forRequest: TRequest, forKey: string) =>
    others(await queryClient.fetchQuery({ queryKey: queryKey(forKey), queryFn: () => check(forRequest), staleTime }));
  const [checking, setChecking] = useState(false);

  // The server's list, while the input it answered for is unchanged.
  const [refused, setRefused] = useState<{ key: string; candidates: TCandidate[] } | null>(null);
  const shown: readonly TCandidate[] =
    refused && refused.key === asked.key ? refused.candidates : asked.request ? others(live.data ?? []) : [];

  // "This is a different one" holds for the records it was said about: a new one shows the choice again.
  const [decision, setDecision] = useState<DuplicateDecision>(undecided);
  const [decidedFor, setDecidedFor] = useState<readonly string[]>([]);
  const decidedOn = (list: readonly TCandidate[]) =>
    decision.reviewed && list.every((candidate) => decidedFor.includes(idOf(candidate)));
  const reviewed = decidedOn(shown);
  // A refusal under the panel belongs to the input it was said for: new typing clears it.
  const [panelError, setPanelError] = useState<{ key: string; message: string } | null>(null);
  const panelMessage = panelError && panelError.key === asked.key ? panelError.message : null;
  const panelRef = useRef<HTMLElement>(null);
  // Each request moves the focus to the panel once it is shown (a refused save shows it in the same render).
  const [focusRequests, setFocusRequests] = useState(0);
  useEffect(() => {
    if (focusRequests > 0) {
      panelRef.current?.focus();
    }
  }, [focusRequests]);
  const stop = (forKey: string, message: string) => {
    setPanelError({ key: forKey, message });
    setFocusRequests((count) => count + 1);
  };

  const decide = (next: DuplicateDecision) => {
    setDecision(next);
    setDecidedFor(next.reviewed ? shown.map(idOf) : []);
    setPanelError(null);
  };

  /**
   * The save decides on the list for exactly what is saved, never on a list for earlier typing: a decision goes with
   * the save only when every record that input matches was decided on.
   */
  const submit = async (
    values: TValues,
    send: (decision: DuplicateDecision, refusedForReview: (error: unknown) => boolean) => void,
  ) => {
    const current = requestFor(values);
    const currentKey = keyOf(current);
    let list: readonly TCandidate[] = [];
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
      stop(currentKey, reviewFirstDefault(noun));
      return;
    }
    setPanelError(null);
    send(list.length > 0 ? { reviewed: true, reason: decision.reason } : undecided, (error) => {
      const candidates = reviewCandidates(error);
      if (!candidates) {
        return false;
      }
      setRefused({ key: currentKey, candidates });
      setDecision(undecided);
      setDecidedFor([]);
      stop(currentKey, `Someone added ${article(noun)} ${noun} that may match. Review the list before saving.`);
      return true;
    });
  };

  return {
    /** The candidates to show (none: no panel). */
    shown,
    /** The panel's decision: undecided again for a new record, the reason typed before kept. */
    decision: reviewed ? decision : { reviewed: false, reason: decision.reason },
    decide,
    panelRef,
    panelMessage,
    /** The save's own check is under way. */
    checking,
    submit,
  };
}
