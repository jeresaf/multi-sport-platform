# MSP — multi-sport competition platform

Multi-tenant SaaS for running sports competitions: registry (people, teams, venues) → competitions and editions
→ fixtures → live scoring with structured play-by-play → result approval → standings → public sites. Leased to
federations, leagues, schools and organisers (Uganda/East Africa first). Flagship sports in build order: football;
rugby 15s and 7s; basketball; then cricket T20 and others.

## How we work

- You are the **builder**. A separate Claude **reviewer** reviews every PR. The human **owner** approves plans,
  answers questions and merges. You never merge.
- One task at a time from `docs/build/slices/`. Start with `/next-task`, finish with `/finish-task`, respond to
  reviews with `/address-review <PR>`. Process details: `docs/build/workflow.md`.
- Plan before multi-file changes and wait for the owner's approval of the plan.
- Branch per task: `s<NN>/t<N>-<slug>`. Conventional Commits with module scopes. Never commit to `main`.
- Update `docs/build/PROGRESS.md` at the end of every task.
- If a spec is ambiguous or conflicts with an ADR or architecture doc: **stop and ask** (AskUserQuestion). Don't
  invent. Unanswered questions go to `docs/build/open-questions.md`.

## Source of truth (highest first)

1. `docs/decisions/` — ADRs (Accepted ones win)
2. `docs/architecture/` and `docs/domain/`
3. The current slice spec in `docs/build/slices/`
4. `docs/reference/` — the earlier plans and selected prototype docs (background only; frozen; may conflict —
   ADRs win)
5. Code and tests

When code and docs disagree, don't silently pick one — raise it.

## The prototype (first attempt)

This repository previously held a prototype, preserved at tag `prototype-v0` (branch `prototype/v0`). Don't port
its code, schemas or patterns, and don't restore its files. You may read it for ideas (`git show
prototype-v0:<path>`). What was kept: `docs/reference/first-attempt/README.md`. Why it was replaced:
`docs/background/first-attempt-code-review.md`.

## Where things are

- Module ownership and boundaries: `docs/architecture/modules.md` (13 modules)
- Rules you must follow: `docs/architecture/rules.md` (AR-01 … AR-33)
- Tenancy: `docs/architecture/multi-tenancy.md` · Authorization: `docs/architecture/authorization.md`
- Sports, timeline, engines: `docs/architecture/sport-engine.md` · definitions: `docs/domain/sports/`
- State machines: `docs/domain/state-machines.md` · formats: `docs/domain/formats.md` · rules: `docs/domain/rulesets.md`
- API: `docs/architecture/api-conventions.md` · data: `docs/architecture/data-conventions.md`
- UX: `docs/product/ux/` · glossary: `docs/product/glossary.md`
- Path-specific conventions load automatically from `.claude/rules/` when you touch matching files.

## Stack

- Backend: .NET 10 (LTS), ASP.NET Core minimal APIs, EF Core 10 + Npgsql, PostgreSQL 17, SignalR, Hangfire
  (only when a slice needs scheduled jobs), Serilog, OpenTelemetry. Tests: xUnit, Shouldly, Testcontainers,
  Respawn, NetArchTest.
- Web: pnpm + Turborepo, Next.js 16 (App Router), React 19, TypeScript strict, Tailwind, shadcn/ui, TanStack
  Query, client generated from OpenAPI, `@microsoft/signalr`, Vitest, Playwright.
- Mobile: Flutter — not before Phase 8.
- Local: Docker Compose (PostgreSQL, Mailpit). CI: GitHub Actions.

## Commands

> Run from the repository root. Keep this block accurate. Web needs Node 24 and `corepack enable` (pnpm is pinned
> in web/package.json). Local infra needs `cp .env.example .env` once.

```bash
dotnet build backend -warnaserror               # backend/SportsPlatform.slnx
dotnet test --solution backend                  # xUnit v3 on MTP; integration tests need Docker (Testcontainers)
dotnet format backend --verify-no-changes       # drop --verify-no-changes to fix formatting
dotnet run --project backend/src/SportsPlatform.Host     # API on http://localhost:5080 (GET /health/live)
dotnet run --project backend/tools/SportsPlatform.Cli   # lists CLI commands; runs as Development (launch profile)
DOTNET_ENVIRONMENT=Staging dotnet run --no-launch-profile --project backend/tools/SportsPlatform.Cli -- migrate   # another environment needs --no-launch-profile
dotnet run --project backend/tools/SportsPlatform.Cli -- migrate   # all module migrations, as sp_migrator
dotnet run --project backend/tools/SportsPlatform.Cli -- seed --category REFERENCE   # also CONFIGURATION|DEMO|TEST; --reset (Development) drops and re-migrates first
MSP_ADMIN_PASSWORD='<choose one>' dotnet run --project backend/tools/SportsPlatform.Cli -- create-platform-admin --email <address>   # never a default
dotnet run backend/tools/reference-data/build-reference-data.cs   # regenerate Platform's reference data files (then seed REFERENCE)
dotnet run --project backend/tools/SportsPlatform.Cli -- export-catalogues   # docs/generated/{actions,roles,errors,events}.md, contracts/{actions,roles,errors,events}.json
dotnet run --project backend/tools/SportsPlatform.Cli -- export-openapi      # contracts/openapi.json (Scalar: http://localhost:5080/scalar)
dotnet tool restore                             # once: dotnet-ef (pinned in .config/dotnet-tools.json)
docker compose up -d                            # local PostgreSQL 17 + Mailpit (UI http://localhost:8025)
infrastructure/local/postgres/check-roles.sh    # verifies sp_migrator / sp_app / sp_platform permissions
docker compose down -v                          # stop and delete local data
cd web && pnpm install --frozen-lockfile && pnpm lint && pnpm typecheck && pnpm test && pnpm build
cd web && pnpm --filter @msp/api-client generate   # TS client from contracts/openapi.json (after export-openapi)
cd web && pnpm --filter @msp/e2e exec playwright install chromium   # once per machine
cd web && pnpm e2e                              # builds both apps; starts the API (migrate, seed DEMO; needs docker compose up); smoke tests on 3000/3001
cd web && pnpm --filter @msp/public dev         # or @msp/admin (port 3001)
```

## Non-negotiables (full list and rationale in docs/architecture/rules.md)

1. Respect module boundaries: reference only other modules' `*.Contracts`; no shared DbContexts, no cross-schema
   queries, no cross-module foreign keys.
2. Tenant isolation: every tenant-owned table has `tenant_id` + RLS policy; tenant comes from auth/host, never the
   request body; new tenant entities are added to the isolation tests. Never use `IgnoreQueryFilters()` in features.
3. No sport-specific branching in generic code. Sport behaviour = definition data + scoring-family engine.
4. Contest facts are append-only timeline events; the score is derived; corrections are new events.
5. Status ≠ phase ≠ result. Only the state machines in `docs/domain/state-machines.md`.
6. Published configuration and approved results are immutable; changes create new versions.
7. Every state-changing endpoint declares an action code; authorization = access mode ∧ entitlement ∧ scoped
   permission ∧ domain rule, enforced server-side.
8. Audit-required actions are audited (append-only); errors use stable codes in Problem Details.
9. History over overwrite: effective dates, versions, status history; no hard deletes of referenced data.
10. DTOs are not entities; public responses contain only `PUBLIC` fields; minors are private by default.
11. No new infrastructure or commercially licensed dependency without an ADR (MediatR, AutoMapper,
    MassTransit, FluentAssertions v8+, ImageSharp are not allowed).
12. Tests are part of the task. Don't skip, weaken or delete tests to get green.
13. Never fake data for unsupported features — show "not available" and record the gap.
14. Never edit `docs/reference/`, an Accepted ADR (except its status line when superseded), or a migration
    that is already on `main`.
15. Time: store UTC instants, inject `TimeProvider`; never `DateTime.Now`.

## Code conventions (short)

- Generic code says `contest`, `person`, `edition`, `entrant` (see glossary); sport words live in definitions,
  engines and renderers.
- Backend use cases are plain handler classes in `Features/<Area>/<UseCase>/`; DbContext is the unit of work.
- Expected failures return `Result`/`Error` with a stable code; exceptions are for bugs.
- Generated files (`docs/generated/*`, `contracts/*`, web API client) are regenerated by tools, never hand-edited.
- New dependency → list it with its licence in the PR.

## Seed and demo data

Use the fictional pilot customer in `docs/product/pilot-customer.md` (Kampala Metro Sports Association, its clubs,
venues and people; second tenant ELSSC). Never use names of real clubs, federations, leagues, venues or people,
and never "Team A", "Player 1" or lorem ipsum. Seeds go through application commands, not raw inserts
(reference and configuration seeds excepted: they write read-only global tables as the owner). Seeding rules:
`docs/architecture/data-conventions.md` (Seeding).

## When compacting

Preserve: current task ID and spec path, files changed, test status and failures, decisions made with the owner,
open questions, and the next step.
