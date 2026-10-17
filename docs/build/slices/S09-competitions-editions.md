# S09 — Competitions and editions (outline)

> **Status: outline.** The reviewer writes the detailed spec before this slice starts.

**Goal:** a competition manager sets up a football league season: competition, edition with frozen rules,
entrants, rosters and the league structure.
**Modules:** Competitions (new), Sports Configuration (resolver, generators), Team Operations, Registry,
Identity (COMPETITION scope).
**Reference:** pack specs 16, 18–22; ADR-0006, ADR-0008; docs/domain/rulesets.md, formats.md, state-machines.md;
docs/product/ux/admin-app.md (wizard and edition dashboard).

## Scope

- **Competition** identity (sport variant, optional governing organisation, slug, logo, lifecycle).
- **Edition wizard** (6 steps) using the S05 resolver; effective-rules snapshot frozen when the edition leaves
  `DRAFT` or fixtures are first generated; `edition.rules.amend` (reason, new snapshot version, future contests
  only); edition lifecycle; quota `MAX_ACTIVE_EDITIONS`.
- **Registrations → entrants:** team entries (direct entry by a competition manager, or submit/approve flow).
- **Rosters** per entrant (ADR-0008): add players from the team's active memberships within edition dates,
  competition shirt numbers, limits from the snapshot, submit/approve (policy), lock (first contest or deadline),
  reopen with reason. Team page tab "Competitions & Rosters".
- **Eligibility v1:** active membership on date, roster limits, age limits with reference date, minors' consent
  per tenant policy; overrides with reason; re-evaluated when inputs change.
- **Structure:** instantiate stages/groups/rounds/slots from the format; draws with stored seed; publish.
- Scope chain provider for `COMPETITION`; edition dashboard with the next-actions checklist.

## Key acceptance

- Create "Metro Football League — 2027" (double round robin, FOOTBALL_LEAGUE with an edited bench size);
  6 entrants; rosters approved; snapshot immutable afterwards (test); amendment creates a new version.
- A competition manager scoped to this competition cannot manage another (test).
- Isolation tests for all new entities.
