# Review of the previous plans — what's kept, what changed, and why

**Reviewed (17 Oct 2026):** the first-attempt summary (`docs/reference/first-summary.md`), the owner's update
notes (`docs/reference/update-notes.md`), and the full ChatGPT greenfield implementation pack
(`docs/reference/chatgpt-pack/`: 43 system specs, 10 control documents, 5 implementation documents, 2 templates).

## Bottom line

The domain thinking across all three documents is good and most of it survives. Several key distinctions are
exactly right: person vs. athlete vs. staff vs. official; organisation vs. team; membership vs. roster vs.
lineup; contest status vs. sport phase vs. result; live score vs. official result; and versioned rules.
**The plan's weakness is its shape, not its ideas.** It splits one product into 43 "systems" (~38 code
modules), leaves the hardest mechanisms unspecified (how scores are actually computed, how tenants are isolated,
who users are), and sequences months of backend before anything is visible. That shape matches how AI-built
projects usually stall. The new kit keeps the ideas, consolidates the structure into **13 modules**, specifies the
missing mechanisms, and switches to **thin end-to-end slices**, each one reviewed.

I can't see the first application's code. The first summary's own warnings ("do not fake invalid data", "do not
invent duplicate concepts", "do not hard-code sports") suggest the usual failure pattern: too much generated at
once, with no running tests and no review loop. The new setup fixes the **process** as well as the spec: Claude
Code runs the code and the tests, every task is a small PR, and a separate reviewer checks each PR against written
rules.

## What's strong and kept

- Multi-tenant leasing model; strict isolation; read-only rather than deletion on expiry.
- Universal **contest** (not "match"); "never assume two teams / one integer score / higher wins / a table exists".
- Separation of **sport**, **format** and **ruleset**; immutable versions; editions bound to exact versions.
- **Person** as canonical identity with role profiles; **organisation ≠ team**; **membership ≠ roster ≠ lineup**.
- **Status ≠ phase ≠ result**; ENDED vs. CLOSED; results approved before standings change.
- Structured **timeline** with categories, importance, and corrections by reversal (never silent edits).
- Audit ≠ logs ≠ analytics; statistics ≠ product analytics; read models are rebuildable.
- Imports go through domain commands; provider IDs are secondary.
- Modular monolith, PostgreSQL, "infrastructure must be earned"; deterministic, realistic seed data.
- The football-league vertical slice as the first real milestone (kept as **M1**).
- The action registry idea, now made enforceable. It is also the answer to the owner's Subscription/Feature
  Manager requirement.

## What needed fixing (and what changed)

| # | Issue in the previous plan | Change | Where |
|---|---|---|---|
| 1 | **Over-decomposition:** 43 systems → ~38 backend modules (up to ~190 projects), many being aggregates of one area. | **13 modules**, aligned with the pack's own schema map; modules are created only when their first slice starts. | ADR-0001, `docs/architecture/modules.md` |
| 2 | **Two sources of truth:** Live Scoring owns a score; Timeline owns events; then "reconcile" commands/errors patch the gap. | **Timeline is the only record**; the score is a pure fold over it; corrections recompute everything; nothing to reconcile. | ADR-0002 |
| 3 | **"No hard-coding" without a mechanism:** `default_points` can't express cricket, tennis, golf or boxing. | **Definitions as data + 5 scoring-family engines** (`ADDITIVE`, `INNINGS`, `SETS`, `RANKED_METRIC`, `COMBAT`). Four of the five flagship sports share `ADDITIVE`. | ADR-0003, `docs/architecture/sport-engine.md` |
| 4 | **Three configuration editors** (sport, format, ruleset) with draft/publish lifecycles and a visual graph builder, before any match can be scored. | Configuration authored **in the repo** (hash-checked); tenants pick a template and edit exposed parameters; editions freeze an **effective-rules snapshot**. Editors later if justified. | ADR-0006, `docs/domain/rulesets.md` |
| 5 | **No users, login, roles or resource scoping** — a team manager limited to *their* team isn't expressible. | ASP.NET Core Identity; **permission = action code**; **scoped role assignments** (tenant/competition/team/contest). | ADR-0005, `docs/architecture/authorization.md` |
| 6 | **Isolation "enforced below controllers" — but how?** | Shared schema + `tenant_id`, EF Core filters **and** PostgreSQL RLS, plus a generic isolation test harness. | ADR-0004, `docs/architecture/multi-tenancy.md` |
| 7 | **Overlapping state machines** (POSTPONED in two modules; EXPIRING/GRACE in tenant *and* subscription; six timeline statuses; CORRECTED status plus versions). | One canonical set; derived states computed (starting soon, rescheduled, subscription status, access mode). | ADR-0007, `docs/domain/state-machines.md` |
| 8 | **Dependency cycle:** rosters in Team Operations depend on editions; registrations depend on rosters. | Rosters belong to Competitions; **the UI stays on the team page** exactly as the owner described. | ADR-0008 |
| 9 | **Missing pieces:** staff team assignments, official contest assignments (`official.assign` missing from the registry), file/photo storage, outbox decision, job runner (Hangfire dropped), discipline (cards → suspensions). | Added: staff assignments (Team Ops), official assignments (Match Ops), media module (Platform), outbox + inbox, Hangfire when needed. Discipline is logged as a later feature. | ADR-0009, ADR-0014, `modules.md` |
| 10 | **Roadmap contradiction:** Phase 7 acceptance needs the public UI, but the UI arrives in Phase 8; months without visible progress. | Vertical slices: each slice ships backend + UI + tests; public pages grow from S02 onwards; M1 is defined precisely. | `docs/build/roadmap.md`, `slices/S14` |
| 11 | **Internal inconsistencies:** three-letter error prefixes contradict the pack's own examples (`PER_` vs `PERSON_…`); `PLATFORM_ADMIN` listed as an entitlement; phase names differ (`Q1` vs `FIRST_QUARTER`); 8 "contest shapes" mixing format concepts (series, fight card) with scoring (complex score); a polymorphic Participant registry. | Readable error prefixes; roles vs. entitlements separated; phases come only from definitions; shapes reduced to `HEAD_TO_HEAD`/`FIELD`; **entrants** in Competitions replace polymorphic participants. | `api-conventions.md`, `glossary.md` |
| 12 | **Uneven spec depth:** specs 03–09 are detailed; 12–43 are 2–3 KB and leave room for guessing. | Detailed specs for S00–S05 now; later slices get detailed specs **just in time**, written by the reviewer from what was actually built. | `docs/build/slices/` |
| 13 | **Written for Codex.** | Retargeted to Claude Code: a short `CLAUDE.md`, path-scoped rules, task skills (`/next-task`, `/finish-task`, `/address-review`, `/new-adr`), a pre-review subagent, a guard hook, a permission policy and a PR template. | `.claude/`, `CLAUDE.md` |
| 14 | **Licensing traps** not considered. MediatR (v13+), AutoMapper (v15+), MassTransit (v9+), FluentAssertions (v8+) and ImageSharp moved to commercial licensing. | Excluded by policy; plain handlers, explicit mapping, outbox, Shouldly, SkiaSharp/Magick.NET. | ADR-0013 |
| 15 | **Privacy of children** and data residency not addressed, although schools and academies are target customers. | Field classification; minors private by default with guardian consent; no medical data; hosting region decided: data stays in South Africa, with a compliance gate before the first pilot (Uganda DPPA 2019). | ADR-0015, ADR-0016 |
| 16 | **Network reality** not considered. Most fans will use mobile data on mid-range Android phones. | Public web is SSR-first with a performance budget; responsive design for phones; Flutter deferred until the API is stable. | ADR-0011/0012, `docs/product/ux/public-app.md` |

## Your open questions, answered

**Schedule Management and Score Management — standalone systems or helpers?**
Neither. They become **one module, Match Operations**, downstream of Competitions. Fixtures, match-day prep, live
scoring, timeline and results all act on the same contest and must stay consistent. Splitting them is what
created the pack's reconciliation workflows. They are not helpers of Player/Team either: players and teams don't
depend on fixtures; fixtures depend on competitions. (Even the pack's database map put them all in one
`operations` schema.) Friendlies and one-off matches use a "Quick match" flow that creates a single-contest
edition behind the scenes, so every contest has the same model.

**History/Statistics Manager — standalone or not?**
Standalone module (**Insights**), but **read-only**. It builds statistics, history, public pages and search from
events and approved results, and can be rebuilt at any time. League tables and brackets get their own small
module (**Standings & Progression**), because advancement (who fills "Winner QF1") is business logic, not just
display.

**Backlog Manager — standalone or not?**
Standalone (**Data Exchange**), renamed **historical backfill**. It never writes tables directly. Past matches are
entered through Match Operations' `contest.record_historical_result` (final score plus optional key events, no live
clock), then approved like any result. Bulk imports use templates (XLSX/JSON) → mapping → validation → duplicate
resolution → dry run → the same commands the UI uses.

**"Each system should expand into a national register wherever possible."**
Yes. The cheap foundations are in from day one: stable IDs, external identifiers with an authority (e.g. FUFA),
aliases, duplicate detection, merge history, provenance and privacy classification. The national features come
later as explicit, governed capabilities: federation tenants, linking local records to national ones with
consent, stewardship and a results archive. They are never implicit data sharing.
See `docs/architecture/national-registry.md`.

**Constant Manager.**
The intent was "no magic values scattered in code", and it is kept. It is split by kind:
- reference data (countries, currencies, time zones) lives in Platform;
- statuses and rules stay in their owning modules;
- tunable values go to rulesets and tenant settings;
- secrets and app config go to environment settings;
- actions, errors and events are **generated catalogues** that CI keeps current.

A single "constants bucket" would recreate the coupling the rest of the design avoids.

**Data Integrity Manager.**
This is a set of conventions enforced by EF Core conventions, database constraints, RLS, architecture tests and
review (`docs/architecture/data-conventions.md`), not a runtime module.

## Your managers → the 13 modules

See the table in `docs/architecture/modules.md` ("Owner's managers → modules"). In short:
- Constant, Seed, Audit, Data Integrity and Analytics → **Platform** (with conventions).
- Sports + Competition Format → **Sports Configuration**.
- Tenant + Subscription/Feature → **Tenancy & Commercial**.
- Branding + Sponsor → **Branding & Sponsorship**.
- Player → **Registry**.
- Team → **Registry + Team Operations** (rosters in **Competitions**, managed from the team page).
- Competition → **Competitions**.
- Schedule + Score → **Match Operations**.
- History/Statistics → **Insights** (+ **Standings & Progression**).
- Backlog → **Data Exchange**.
- New: **Identity & Access** (users and roles) and **Engagement** (notifications and follows).

## What I could not verify

- ~~The first application's code and why exactly it failed.~~ Reviewed on 2026-10-17 once the repository was
  shared: docs/background/first-attempt-code-review.md.
- Sport rules: the definitions follow widely used international laws (IFAB, FIBA, World Rugby, MCC/T20 playing
  conditions) as I understand them. Local competitions differ, so each definition and template needs review
  against the customer's regulations before a pilot (open question Q9).
- Legal obligations: the privacy design follows Uganda's Data Protection and Privacy Act, 2019 at a high level
  (children's consent, special data, cross-border storage, registration with the regulator). It is not legal
  advice; get a lawyer's view before real data is stored.

## Decisions (updated 2026-10-17)

The owner answered the first four questions (details in `docs/build/open-questions.md` → Answered):
- **Q1:** each customer gets its own public site (ADR-0010).
- **Q2:** delegated to the reviewer — STANDARD and PRO plans, event pass, Enterprise by overrides, indicative
  prices (`docs/product/pricing-and-plans.md`).
- **Q4:** outside Uganda — Azure South Africa North behind Cloudflare, data kept in South Africa (ADR-0016).
- **Q5:** no customer yet — a fictional pilot customer (`docs/product/pilot-customer.md`); sport order football →
  rugby → basketball → others.
