# S11 — Match day (outline)

> **Status: outline.** The reviewer writes the detailed spec before this slice starts.

**Goal:** everything needed before kick-off — officials, lineups, readiness — and the contest status machine.
**Modules:** Match Operations, Competitions (rosters), Registry (officials), Identity (CONTEST scope).
**Reference:** pack specs 24, 25; docs/domain/state-machines.md (contest, lineup, match-day flow).

## Scope

- **Official assignments** by sport official role; minimums from the snapshot; conflicts (same official,
  overlapping contests); capability check against official profiles.
- **Lineups** from eligible roster entries: starters/bench counts and position constraints (snapshot +
  definition), captain; submit → approve (policy) → lock at start; late change with reason before start.
- **Readiness** checklist from rules (lineups approved, officials minimum, venue set, participants confirmed);
  override with reason.
- **Contest status machine** (start, delay, postpone, cancel, interrupt, resume, suspend, abandon, end, reopen)
  with status history and reasons; derived "starting soon".
- Scope chain provider for `CONTEST`; `SCORER` assignable per contest.
- Admin: match board; contest operations page (hero, readiness, lineups, officials, status controls, audit).

## Key acceptance

- A contest cannot start until readiness passes or is overridden with a reason.
- Lineup validation follows the snapshot (e.g. 11 starters, exactly 1 goalkeeper, bench ≤ 9).
- Invalid status transitions are rejected; every transition is audited.
