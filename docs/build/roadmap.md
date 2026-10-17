# Roadmap — phases, slices and milestones

**How to read this.** Work is cut into **slices**: thin, end-to-end increments (backend + UI + tests) that end
in something demonstrable. Each slice contains **tasks**; **one task = one branch = one pull request**, reviewed
before merge. Slices S00–S05 have detailed specs; S06–S14 have outlines that the reviewer turns into detailed
specs just before they start (so specs reflect what was actually built). Later phases are listed here only.

Status legend: ☐ not started · ◐ in progress · ☑ done. Claude Code updates this file when a slice completes.

## Phase 0 — Foundations

| Slice | Goal | Spec | Status |
|---|---|---|---|
| S00 | Repo, tooling, CI: an empty-but-real monorepo that builds and tests | [S00](slices/S00-repo-foundation.md) | ◐ |
| S01 | Backend platform: modules, persistence, errors, actions, audit, outbox, test harness | [S01](slices/S01-backend-platform.md) | ☐ |
| S02 | Tenancy core & isolation: tenants, domains, tenant context, EF filters + RLS, isolation harness, public shell | [S02](slices/S02-tenancy-core.md) | ☐ |
| S03 | Identity & access: users, login, memberships, scoped roles, admin shell, platform console basics | [S03](slices/S03-identity-access.md) | ☐ |
| S04 | Plans & entitlements: plans, subscriptions, access mode, full authorization pipeline, capabilities API | [S04](slices/S04-plans-entitlements.md) | ☐ |

## Phase 1 — Configuration

| Slice | Goal | Spec | Status |
|---|---|---|---|
| S05 | Sports configuration: football definition, ADDITIVE engine, formats (single, round robin, knockout), ruleset templates, tenant sport enablement | [S05](slices/S05-sports-configuration.md) | ☐ |

## Phase 2 — Registry and teams

| Slice | Goal | Spec | Status |
|---|---|---|---|
| S06 | People registry: persons, duplicate prevention, athlete/staff/official profiles, minors' consent, media | [S06](slices/S06-people-registry.md) (outline) | ☐ |
| S07 | Organisations, teams, venues | [S07](slices/S07-organisations-teams-venues.md) (outline) | ☐ |
| S08 | Team operations: player memberships, staff assignments, the team-page flow | [S08](slices/S08-team-operations.md) (outline) | ☐ |

## Phase 3 — Competition setup

| Slice | Goal | Spec | Status |
|---|---|---|---|
| S09 | Competitions & editions: wizard, rules snapshot, registrations/entrants, rosters, eligibility v1, league structure | [S09](slices/S09-competitions-editions.md) (outline) | ☐ |
| S10 | Fixtures: generation, dates/venues, conflicts, publish, reschedule history, public fixtures list | [S10](slices/S10-fixtures-schedule.md) (outline) | ☐ |

## Phase 4 — Match day: the football vertical slice

| Slice | Goal | Spec | Status |
|---|---|---|---|
| S11 | Match day: officials, lineups, readiness, contest status machine | [S11](slices/S11-match-day.md) (outline) | ☐ |
| S12 | Live scoring & timeline: scorer workspace, engine fold, corrections, SignalR, live public match page | [S12](slices/S12-live-scoring-timeline.md) (outline) | ☐ |
| S13 | Results & standings: submit/approve, correction flow, standings with tiebreakers, audit explorer | [S13](slices/S13-results-standings.md) (outline) | ☐ |
| S14 | Public web v1: home, live, competition, match, team, player pages; responsive; E2E acceptance | [S14](slices/S14-public-web-v1.md) (outline) | ☐ |

**Milestone M1 — Football league end to end.** The acceptance scenario in
[S14](slices/S14-public-web-v1.md) passes as an automated Playwright test plus a manual demo, with no manual
database edits.

## Phase 5 — Multi-sport proof

Order follows the owner's priority — **football → rugby → basketball → others** — and the pilot persona
(docs/product/pilot-customer.md).

| Slice | Goal | Status |
|---|---|---|
| S15 | Rugby 15s and 7s: bonus points, sin bin, sudden death, GROUPS_THEN_KNOCKOUT, sevens festival day | ☐ |
| S16 | Basketball: quarters/overtime, fouls, timeouts, SERIES format for playoffs | ☐ |
| S17 | Cricket T20 (first of the "other" sports): INNINGS engine, delivery pad, scorecards, net run rate, super over | ☐ |

**Milestone M2 — Generic architecture proven.** Rugby and basketball need only definitions, templates and tests
(no engine changes); cricket adds one engine. Any change to generic modules requires an ADR explaining why.

Outlines (detailed specs are written before each starts):
- **S15 Rugby** — `RUGBY_UNION_15` and `RUGBY_UNION_7` definitions + templates; conversion follow-up rule;
  sin-bin timers in the scorer UI; try/losing bonus points; `TRIES_SCORED`/`TRY_DIFFERENCE` tiebreakers;
  sudden-death extra time; GROUPS_THEN_KNOCKOUT generator with cross-over pairing (Cup and Plate); sevens
  festival-day view (many short matches on one day). Persona: Metro Rugby League and Metro Sevens.
- **S16 Basketball** — `BASKETBALL_STANDARD` definition + templates; personal/technical foul counts and
  disqualification; timeout limits; `REPEAT_EXTRA_PERIOD`; count-down clock with stop/start; SERIES generator
  and playoff bracket; box-score renderer; scenarios (double overtime, foul-out, series 2–1). Persona: Metro
  Basketball League.
- **S17 Cricket** — `CRICKET_T20` definition + template; INNINGS engine with every rule in
  docs/architecture/sport-engine.md §6 covered by scenarios; delivery pad; batting/bowling scorecards; NRR;
  super over (repeat); `OVER_SUMMARY` basic mode; manual revised target. Persona: Metro Corporate T20 Cup.
- **Other sports after S17** — netball (the persona's 4th sport and its reason to move to PRO), volleyball and
  hockey are definitions on existing engines (Phase 9). If a paying customer needs one of them first, the owner
  may swap it in for S17.

**Infrastructure task after M1 (before pilots):** infrastructure as code, the Azure staging environment and the
deployment workflow (ADR-0016). Production and the compliance pilot gate (ADR-0016 point 6) come before the
first pilot stores real personal data.

## Phase 6 — Data and insights

| Slice | Goal | Status |
|---|---|---|
| S18 | Statistics & history: player/team stats from timelines, leaders, careers, champions | ☐ |
| S19 | Historical backfill & bulk import: past-results entry, XLSX/JSON templates, dry run, provenance | ☐ |
| S20 | Search (PostgreSQL full-text + trigram), reports (CSV/XLSX), tenant dashboards | ☐ |

## Phase 7 — Engagement and commercial

| Slice | Goal | Status |
|---|---|---|
| S21 | Notifications & reminders (Hangfire), public accounts, follows, preferences | ☐ |
| S22 | Branding & custom domains (brand versions, contrast checks, domain verification) | ☐ |
| S23 | Sponsorship (sponsors, campaigns, placements, sponsor-free mode, ad slots) | ☐ |
| S24 | Transfers & loans (windows, approvals, roster effects) | ☐ |
| S25 | Person merge & stewardship tools; data-subject export | ☐ |

## Phase 8 — Mobile

| Slice | Goal | Status |
|---|---|---|
| S26 | Flutter supporter app (public screens, white-label flavours) — re-confirm framework first (ADR-0012) | ☐ |
| S27 | Offline scorer (local queue, scorer lease, sync, rejection UX) | ☐ |

## Phase 9 — Expansion and hardening

Extended sports and engines (SETS, RANKED_METRIC, COMBAT; netball/hockey via ADDITIVE) · later formats (pools
with placement, two-leg ties, leaderboards, heats, fight cards) · load tests (k6) · security review ·
backup/restore drill · monitoring and alerts · retention and privacy tooling · production hardening on Azure
(ADR-0016) · pilot-readiness checklist · national-registry pilot (docs/architecture/national-registry.md).
