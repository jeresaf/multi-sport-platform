# ADR-0002 — The timeline is the single source of truth for contest facts; the score is derived

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The previous plan gave "Live Scoring" its own score state and "Timeline" its own event stream, owned by
different modules, then added commands and errors to reconcile them (`score.reconcile`,
`ScoreConflictDetected`, `RESULT_NOT_RECONCILED`). Two stores for one truth will drift, especially with
offline scorers, corrections and VAR/TMO reversals.

## Decision

1. Every in-contest fact (period start/end, goal, try, card, substitution, delivery, clock adjustment,
   review decision) is recorded as an **append-only timeline event** in Match Operations.
2. Recorded events are **never updated or deleted**. Fixes are new events:
   - `supersedes_event_id` — a correction that replaces an earlier event (e.g. wrong scorer).
   - `reverses_event_id` — cancels an earlier event's effect while keeping it visible (e.g. VAR overturn,
     entered in error).
   An event's *effective status* is derived: `ACTIVE`, `SUPERSEDED` or `REVERSED`.
3. The **score is a pure function** of the active events:
   `score = engine.Fold(definition, effectiveRules, activeEventsOrderedBySequence)` (see ADR-0003).
4. A score snapshot (`operations.contest_score_snapshots`) is written in the same transaction as each event
   for fast reads. It is a cache: it can be deleted and rebuilt from the timeline at any time.
5. The current sport phase (e.g. `SECOND_HALF`) is likewise derived from `PERIOD` events and stored on the
   contest for convenience.
6. Each event carries a `client_event_id` (unique per contest) so retries and offline sync are idempotent.
   The server assigns the authoritative `sequence`.
7. The official **result** (Results & Approval, inside Match Operations) is proposed from the derived score
   and the engine's outcome logic, then approved by a person. Approved results are versioned and immutable.

## Alternatives considered

- **Separate score store + reconciliation (pack).** Rejected: dual truth, extra workflows, conflict states.
- **Full event sourcing for the whole platform.** Rejected: unnecessary outside the contest timeline;
  ordinary relational aggregates are simpler elsewhere.
- **Mutable score with an audit log.** Rejected: corrections and replays become guesswork.

## Consequences

- Positive: no reconciliation feature exists because none is needed; corrections recompute everything.
- Positive: engines are pure functions — easy to unit test exhaustively per sport.
- Positive: offline sync becomes "append events idempotently", not "merge two scores".
- Negative: reads that need the score must use the snapshot or fold; the snapshot keeps this cheap.
- Statistics (Insights) and standings (Progression) consume timeline/result events — they never write back.

## Rules affected

AR-08, AR-09, AR-10.

## Review trigger

A sport whose scoring cannot be expressed as a fold over events (none known; judged sports like boxing use
judges' scorecards recorded as events).
