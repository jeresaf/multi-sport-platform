# Review of the first attempt's code

**Reviewed:** 2026-10-17, by the reviewer (Claude), read-only. Nothing was built or run.
**Repository:** `jeresaf/multi-sport-platform`, branch `main`, commit `8e72a36` ("add live scoring player roster
and attribution controls").

## Verdict

**Don't build on it. Restart in the same repository with this kit.** Preserve the prototype at tag
`prototype-v0` and branch `prototype/v0`, and carry over only a few documents
(docs/reference/first-attempt/README.md). The prototype has many features, but its foundations — tenancy, module
boundaries, statuses, the source of truth for scores — would each need rework that touches most files, and the
core user journey was never proven end to end. Rebuilding those foundations deliberately (S00–S05) and then one
football league end to end (M1) is cheaper and safer than refactoring.

## What is there

| Area | Size (C#, TypeScript, Dart lines) | Notes |
|---|---|---|
| `backend/Infrastructure` | ~52,000 | One `SportsPlatformDbContext` with 59 `DbSet`s, services, seed data, workers |
| `backend/Modules` | ~21,000 | 21 modules: entities, contracts, sport services |
| `apps/api` | ~26,000 | Controllers, middleware, SignalR hub |
| `apps/admin-web`, `apps/public-web` | ~14,000 and ~4,500 | Next.js 16, React 19, Tailwind 3.4 |
| `apps/mobile` | ~27,000 | Flutter (Riverpod, Drift, Dio, SignalR, Firebase) |
| `tests` | ~16,000 | Includes `tests/IntegrationTests/ApiWorkflowTests.cs` at 7,898 lines |

778 tracked files, 29 projects on .NET 9, 8 EF Core migrations.

## Findings

Each finding lists the evidence and how the kit prevents it.

1. **Breadth before depth; the core journey was never proven.** Football, basketball, rugby, cricket, golf,
   athletics, combat sports, sponsorship, notifications and a mobile app all exist in some form, but the
   empty-database UI walkthrough recorded only `ENV-001` as passing (22 August 2026); the next case, `AUTH-001`
   (sign in), was never run. The large integration tests prove API sequences, not that a person can run a
   league through the UI.
   *Kit:* M1 is one football league end to end, automated in Playwright, with no database edits (S14).

2. **Fat controllers and one shared DbContext.** `CompetitionsController.cs` 4,144 lines,
   `PublicController.cs` 2,902, `LiveScoringController.cs` 1,730, `ResultsController.cs` 1,274,
   `CricketController.cs` 1,167; `DatabaseExtensions.cs` 1,716. All modules share one DbContext, so the
   prototype's own architecture document calls module separation "a code ownership boundary, not independent
   persistence".
   *Kit:* 13 modules, each with its own schema and DbContext, handlers per use case, architecture tests
   (ADR-0001, AR rules).

3. **Sport-specific code in generic places.** Examples: `item.SportId == CricketController.CricketSportId` in
   `PublicController.cs` and `CricketTournamentProjection.cs`; contest shapes such as `"AthleticsSemifinal"`;
   sport-specific controllers and ruleset services; more than 70 sport-code string literals in the API,
   infrastructure and core modules.
   *Kit:* sport behaviour = definition data + scoring-family engine (ADR-0003); an architecture test bans sport
   codes outside definitions, engines, renderers and tests (S05-T5).

4. **Statuses mixed together.** `ContestStatus` combines lifecycle and result states (`Draft, Scheduled,
   CheckInOpen, Ready, InProgress, Paused, Suspended, Completed, PendingApproval, Approved, Corrected, Published,
   Cancelled, Abandoned`), with a `CanonicalContestStatus` projection added "during migration" and two result
   status enums.
   *Kit:* separate, non-overlapping state machines (ADR-0007, docs/domain/state-machines.md).

5. **Two sources of truth for scores.** A `LiveScore` aggregate is kept alongside the timeline.
   *Kit:* the timeline is the only truth; the score is a fold over events; corrections are new events
   (ADR-0002).

6. **Tenant isolation by convention.** 215 hand-written `TenantId ==` checks; no EF global query filters (the
   only `HasQueryFilter` line is commented out); no PostgreSQL row-level security; tenants resolved from claims
   and headers, not from the site's host name.
   *Kit:* global filters + save interceptor + RLS + an isolation test harness (ADR-0004); host-based tenant
   sites (ADR-0010).

7. **Demo data shaping the product.** Public pages read presentation values (stage, period, clock, summary)
   and standings rows authored in CSV files, kept in step by start-up "reconcile" code.
   *Kit:* seeds go through application commands; read models are rebuilt from facts; unsupported data shows
   "not available" (AR-22, AR-30).

8. **Hard-coded passwords.** Bootstrap and tenant-admin passwords are written into seed code
   (`SeedData.cs:102`, `WalkthroughSeedData.cs:55`, `DatabaseExtensions.cs:1005`), dozens of tests and several
   docs. **If any environment reachable from a network ever used them, change those passwords now.**
   *Kit:* no default passwords; the bootstrap admin comes from environment configuration or a secret store;
   `.env` files are unreadable to Claude Code.

9. **Platform and dependencies.** .NET 9 reaches end of support on 10 November 2026; the deprecated
   `Microsoft.AspNetCore.Mvc.Versioning` 5.1.0; ASP.NET Core 2.3.0 abstraction packages referenced from .NET 9
   projects.
   *Kit:* .NET 10 LTS and a dependency licence policy (ADR-0013).

## What the prototype got right

The ideas survive in the kit; the code does not.

- A modular monolith, one universal contest model, versioned rulesets, SignalR published only after
  persistence, append-only audit protected by a database trigger, and an ordered, idempotent offline queue —
  all present in the kit's ADRs.
- Operations runbooks and the empty-database use-case catalogue — copied to docs/reference/first-attempt/.
- Screenshot checks at seven viewport sizes, including 390 px phones — kept as a habit for UI tasks.
- Basketball and rugby timeline packages
  (`backend/Modules/SportsPlatform.Modules.Sports/Services/BasketballTimelinePackage.cs`,
  `RugbyTimelinePackage.cs` at tag `prototype-v0`) — worth comparing with docs/domain/sports/*.yaml when the
  reviewer writes the S15 and S16 specs.

## Why restart rather than refactor

- Fixing findings 2, 4, 5 and 6 changes the data model, the persistence layer and almost every controller at
  once, and the 7,898-line workflow test encodes the old behaviour, so it would have to be rewritten too.
- The .NET 9 → 10 upgrade would come on top.
- The kit's first slices rebuild exactly those foundations, with tests, in small reviewed steps.
- Nothing is lost: the tag and branch keep every line, and `git show prototype-v0:<path>` retrieves any file.

## How the restart works

Steps for the owner are in README-FIRST.md ("Using your existing repository"). In short: tag and branch the
prototype, replace `main`'s contents with the kit through one pull request, then start S00. Claude Code must
not port prototype code or patterns (CLAUDE.md).
