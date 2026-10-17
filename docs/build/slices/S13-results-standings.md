# S13 — Results and standings (outline)

> **Status: outline.** The reviewer writes the detailed spec before this slice starts.

**Goal:** official results with approval and versioned corrections drive league tables automatically, and every
step is traceable.
**Modules:** Match Operations (results), Standings & Progression (new), Insights (read models), Platform (audit
explorer).
**Reference:** pack specs 28, 29; ADR-0002, ADR-0007; docs/domain/rulesets.md (tiebreakers);
docs/build/open-questions.md Q8.

## Scope

- On contest end: result DRAFT proposed by the engine (outcome, method, period breakdown); submit (scorer),
  approve/reject (results officer); contest `CLOSED` on approval; walkover/forfeit with rules forfeit score.
- Result versions; reopen with reason → new draft while the previous approved version stays official;
  `ResultApproved`, `ResultSuperseded` events; optional per-edition `auto_approve_results` (Q8).
- **Standings & Progression:** tables per stage/group from approved results only; snapshot points; tiebreakers
  with head-to-head mini-table semantics; deterministic recalculation (hash); unresolved ties flagged; override
  with reason; knockout slot resolution (`SlotResolved`) for cups.
- Public "Table" tab; admin results inbox and correction workflow; tenant audit explorer (filter by entity,
  actor, action, correlation ID).
- Minimal top-scorers projection for the competition overview (generalised in S18).

## Key acceptance

- Approving a result updates the table; reopening, correcting and re-approving recalculates it and keeps the
  version history; live (unapproved) scores never affect tables.
- Two-way and three-way head-to-head tie scenarios produce the expected order.
- The audit explorer traces a match from fixture publication to final approval.
