# Progress log

Claude Code updates this file at the end of **every** task (keep it short and factual). The reviewer reads it
at the start of every review.

## Now

- **Current slice:** S01 — Backend platform (docs/build/slices/S01-backend-platform.md). S00 is on `main`; its
  "new contributor in under 30 minutes" check is still with the owner
- **Current task:** none. S01-T3b (logging, OpenTelemetry, `/health/ready`) is in review as #17
- **Next:** S01-T3c (OpenAPI, Scalar, TS client, `Contracts` CI job), then S01-T4
- **Blocked on:** nothing

## Next up

1. S01-T3c — OpenAPI, Scalar, `export-openapi`, TS client, `Contracts` CI job
2. S01-T4 — Action catalogue and authorization skeleton
3. S01-T5 — Domain events, outbox and inbox

## Log

| Date | Task | PR | Summary | Notes / deviations |
|---|---|---|---|---|
| 2026-10-17 | — | — | Kit added: CLAUDE.md, .claude/, docs/ | Handoff from reviewer |
| 2026-10-17 | — | — | Kit v2: owner answers Q1, Q2, Q4, Q5; ADR-0016 (hosting); plans and pricing; fictional pilot customer; roadmap Phase 5 reordered (rugby, basketball, cricket); prototype review and selected prototype docs | Restart in the existing repo; prototype at tag `prototype-v0` |
| 2026-10-17 | S00-T1 | #7 | Root conventions (editorconfig, gitattributes, gitignore, global.json, README); `backend/SportsPlatform.slnx` with Host (`GET /health/live`), BuildingBlocks, Unit/Integration/Architecture tests, CLI; central package management; CLAUDE.md commands | xUnit v3 on Microsoft.Testing.Platform → test command is `dotnet test --solution backend` (agreed) |
| 2026-10-17 | S00-T2 | #8 (reached `main` via #10) | `web/` pnpm 12 + Turborepo workspace; Next.js 16.4 `apps/public` (3000) and `apps/admin` (3001) with Tailwind 4; `packages/config` (tsconfig, ESLint flat config with a colour-literal rule, shared Tailwind theme), `packages/tokens` (CSS variables light/dark + TS export), `packages/ui` (`Button` + Vitest), `packages/api-client` placeholder; Playwright smoke tests in `web/e2e` | Tailwind 4 shared `theme.css` instead of a preset; placeholder token colours (both agreed). ESLint 9 (eslint-config-next plugins don't support 10); TypeScript 6.0 (typescript-eslint needs < 6.1) |
| 2026-10-17 | S00-T3 | #9 (reached `main` via #10) | `docker-compose.yml`: `postgres:17` (named volume, TCP healthcheck, init script creating `sportsplatform` and roles `sp_migrator`/`sp_app`/`sp_platform` with default privileges for future schemas), Mailpit v1.31.6, optional `observability` profile (Aspire dashboard 13.6); `.env.example`; `infrastructure/local/postgres/check-roles.sh`; README Local setup | Roles get privileges via `ALTER DEFAULT PRIVILEGES FOR ROLE sp_migrator`, so S01+ migrations need no grant boilerplate |
| 2026-10-17 | S00-T4 | #10 (also landed T2 and T3 on `main`) | `.github/workflows/ci.yml`: backend (restore, build `-warnaserror`, test, format), web (frozen install, lint, typecheck, test, build), e2e (Playwright Chromium, smoke tests against production builds, report artifact on failure), infra (compose up `--wait`, role checks); NuGet and pnpm caches; superseded PR runs cancelled, every push to `main` keeps its own run | Added an `infra` job (not in the spec) so T3's role guarantees are regression-tested |
| 2026-10-17 | S01-T1 | #13 | BuildingBlocks primitives (`IModule`, `ApiEndpointGroups`, `Result`/`Error` with `AREA_CONDITION` check, `Entity`/`AggregateRoot` + domain events, `ITenantOwned`, `ICurrentActor` + `SystemActor`, `ITenantContext`, `ActionCode` + endpoint action metadata); Platform module + Contracts; Host module registry and `/api/{public,admin,platform}/v1` groups; NetArchTest architecture tests (AR-02, AR-26, AR-33, Domain layering) replace the S00 placeholders | BuildingBlocks references ASP.NET Core for `IModule` only (agreed, see Deviations). NetArchTest.Rules 1.3.2 (MIT) |
| 2026-10-17 | S01-T2a | #14 | `SportsPlatform.BuildingBlocks.Persistence`: `ModuleDbContext` (schema, snake_case, app UUIDv7 keys, shadow audit columns via `AuditColumnsInterceptor`, `xmin` row version on aggregates, `SaveChangesResultAsync` → `<MODULE>_CONCURRENCY_CONFLICT`), `App`/`Migrator`/`Platform` connection strings, `MigrationRunner`; `PlatformDbContext` + initial migration; CLI `migrate`; Development auto-migrate; `dotnet-ef` tool manifest; Testcontainers harness (compose image + `01-roles.sh`, app as `sp_app`, Respawn on a fixture-only superuser connection, `FakeTimeProvider`, `TestActor`); `Entity()` no longer generates IDs (#13 R3) | T2 split into T2a/T2b (agreed). EF migration names keep `yyyyMMddHHmmss_`. Pre-review caught `WebApplicationFactory` defaulting to Development (would migrate localhost) → `HostOnlyApp` + guard test |
| 2026-10-17 | S01-T2b | #15 | Migration helpers `ReadOnlyForRuntimeRoles` and `AppendOnly` (grants + per-schema `reject_append_only_change()` triggers rejecting UPDATE, DELETE and TRUNCATE for every role); `migrate` revokes runtime access to each `__ef_migrations_history`; `TableAccessPolicies` + `TableGrantsTests` (every table, view, matview and foreign table; column grants included); reset as unpooled superuser with `session_replication_role = replica`; `check-roles.sh` `sp_platform` DDL and TRUNCATE checks; test-only `reference_probes`/`append_only_probes` | Read-only also covers `sp_platform`; trigger also blocks TRUNCATE (both owner-agreed) |
| 2026-10-17 | S01-T3a | #16 | `SportsPlatform.BuildingBlocks.Api` (`ErrorKind` → status, `Error.ToProblem()`, `Result.ToHttpResult()`); `CommonErrors` for pipeline failures; Host `AddApiConventions`/`UseApiConventions` (Problem Details with `code`, `type`, `correlationId`; exception handler → 500 `PLATFORM_UNEXPECTED_ERROR` without internals; routing/binding failures coded); `X-Correlation-Id` middleware; `ErrorCatalogue` + CLI `export-catalogues` → `docs/generated/errors.md`, `contracts/errors.json` | T3 split in three (owner). Added `VALIDATION_UNSUPPORTED_MEDIA_TYPE` (415) and `ErrorKind.MethodNotAllowed`/`UnsupportedMediaType`/`Unexpected`. Stale-file CI check comes with T3c |
| 2026-10-17 | S01-T3b | #17 | Serilog JSON console logs with `CorrelationId`, `ActorKind`, `ActorId`, `TenantId` (`RequestContextEnricher`) and one completion event per request; OpenTelemetry traces and metrics (ASP.NET Core, HttpClient, Npgsql; OTLP only when configured; probes not traced); `GET /health/ready` (database on `App`, status-only body); `VALIDATION_REQUEST_TOO_LARGE` (413); relaxed escaping in `contracts/errors.json` (#16 R1) | JSON in every environment, no OTLP logs, 413 code with 408/431 documented as body-less (owner). Pre-review: real-Kestrel 413 test, enricher re-entry guard. Review: ASP.NET Core `RequestId` dropped from logs (R2) |

## Deviations from spec (agreed)

- **S00-T1:** test command `dotnet test --solution backend` instead of `dotnet test backend`. xUnit v3 4.x needs
  Microsoft.Testing.Platform on the .NET 10 SDK (selected in `global.json`). Owner approved 2026-10-17. Noted in
  the S00 spec's Implementation notes.
- **S00-T2:** Tailwind 4 has no JS presets, so `web/packages/config` ships a shared `theme.css` (`@theme` mapped to
  the tokens) instead of a "Tailwind preset". Token colour values are placeholders (design-system.md names the
  groups but not the values) until the owner or the Branding work sets them. Owner approved 2026-10-17.
- **S00-T4:** an extra `infra` CI job (compose up + `check-roles.sh`) beyond the spec's backend/web/e2e jobs, so
  T3's role guarantees are regression-tested. Owner approved the plan 2026-10-17.
- **S01-T1:** BuildingBlocks has a `FrameworkReference` to `Microsoft.AspNetCore.App` because `IModule.MapEndpoints`
  needs `IEndpointRouteBuilder`. Only the `SportsPlatform.BuildingBlocks.Modules` namespace may use ASP.NET Core;
  no BuildingBlocks namespace may use EF Core (`LayeringTests`). This replaces S00's "BuildingBlocks references no
  ASP.NET Core" placeholder test. Owner approved the plan 2026-10-17.
- **S01-T2:** delivered as two PRs (T2a conventions + harness, T2b grant narrowing). Migration files keep EF's
  `yyyyMMddHHmmss_` prefix; docs/architecture/data-conventions.md updated. A published CLI defaults to Production
  (`dotnet run` uses Development via launchSettings). The planned "EF Core only in module `Infrastructure`"
  architecture rule was dropped (handlers use the DbContext as unit of work); "Contracts don't use EF Core" added
  instead. Owner approved the plan 2026-10-17.
- **S01-T2b:** `ReadOnlyForRuntimeRoles` revokes writes from `sp_platform` as well as `sp_app` (the spec named
  `sp_app` only; reviewer advice on #14). A platform service that must write a global table is declared as an
  exception, with a reason, in `TableAccessPolicies`. The `AppendOnly` trigger also blocks `TRUNCATE`. Owner
  approved 2026-10-17.
- **S01-T3:** delivered as three PRs (T3a errors and correlation; T3b logging, OpenTelemetry, health; T3c OpenAPI,
  client, `Contracts` CI job). `Result` → HTTP helpers live in `SportsPlatform.BuildingBlocks.Api`, a second
  BuildingBlocks namespace allowed to use ASP.NET Core (`LayeringTests`). T3a adds `VALIDATION_UNSUPPORTED_MEDIA_TYPE`
  (415) beyond the four approved pipeline codes. Owner approved the plan 2026-10-17. T3b adds
  `VALIDATION_REQUEST_TOO_LARGE` (413) and `ErrorKind.PayloadTooLarge`; JSON logs in every environment; traces and
  metrics (not logs) over OTLP. Owner approved the plan 2026-10-17.

## Tech debt / follow-ups

Consolidated by the S00 slice review (2026-10-17). Each item names where it gets done.

**Assigned to S01** (see the spec's "Carried over from S00")
- Mark `GET /health/live` and `/health/ready` with `.AllowPublic()`; the startup test covers every endpoint (T4).
- Apply the T2b grant helpers to the real tables: outbox/inbox (T5, read-write), `audit.audit_entries`
  (`AppendOnly`, T6), reference tables (`ReadOnlyForRuntimeRoles`, T7); declare each in `TableAccessPolicies`.
- `xmin`/`updated_*` cover only the aggregate root's row: a change to only a child entity or owned type doesn't
  bump the root's version, so two edits to different children both succeed. Mark the root `Modified` when a
  dependent changes, with a test, before the first aggregate with child tables (S06 at the latest) (S01-T2a
  pre-review).
- `ITenantOwned.TenantId` stays get-only: S02's interceptor sets it with `entry.Property(...).CurrentValue`, never
  a public setter. Moving `BuildingBlocks/Modules` to a `BuildingBlocks.Web` project is optional, not planned
  (reviewer guidance on #13).
- The per-test reset will wipe reference data (countries, currencies…): exclude those tables from Respawn or
  re-run the reference seed after each reset (T7; reviewer note on #14).
- `seed --reset` (Development, T7) can't TRUNCATE or DELETE append-only tables as `sp_migrator` (the triggers stop
  the owner too): reset by recreating the database or skip append-only tables (T7; S01-T2b).
- 401/403 still produce Problem Details without a `code`; codes come with T4/S03 (S01-T3a pre-review).
- `ICurrentActor` is still the singleton `SystemActor`, so every HTTP request (anonymous ones included) is logged as
  `ActorKind=System`. Add a per-request actor with an anonymous kind for unauthenticated requests and update
  `RequestLoggingTests.Request_is_logged_with_correlation_ID_and_actor` (S03, or T4 if it needs one; #17 R1).
- `/health/ready` is anonymous and runs `SELECT 1` on every hit: expose `/health/*` only to the load balancer and
  probes, or rate-limit them at the edge (infrastructure task after M1, ADR-0016; #17 R3).
- `ProblemDetails:TypeBaseUri` is the placeholder `https://errors.sportsplatform.invalid/` until Q10 (domain).

**Owner decisions / housekeeping**
- `main` has no branch protection: GitHub doesn't offer it for private repositories on the Free plan. Options:
  GitHub Pro (then require PR + checks `Backend`, `Web`, `E2E (Playwright)`, `Local infrastructure`, later
  `Contracts`), or accept the risk (Claude Code's settings already deny pushes to `main`).
- Merge style: README-FIRST says squash-and-merge; S00 used merge commits. Pick one. For stacked PRs, retarget
  the next PR to `main` before merging it.
- The prototype's old branches (`agent/*`, `codex/*`, `feature/*`, `ci/*`) are not all covered by tag
  `prototype-v0`; keep them or tag them before deleting. (The merged S00 branches and `restart/kit` were deleted
  on 2026-10-17.)
- Owner's "new contributor in under 30 minutes" check (S00 slice acceptance); then roadmap S00 → ☑.

**Later**
- Token colour values are placeholders (`web/packages/tokens/tokens.css`); owner/Branding (S22) to confirm. The
  dark palette is written twice in that file; keep both blocks in sync.
- ESLint stays on 9.x until eslint-config-next's plugins support ESLint 10; TypeScript stays below 6.1 until
  typescript-eslint supports it.
- shadcn/ui is not initialised yet (ADR-0011); do it with the first real admin UI (S03). `Button` is hand-written.
- Colour-literal checks have known gaps and false positives (S00 Implementation notes, T2).
- pnpm 12's minimum-release-age policy is on: new package versions are installable about a day after release.
- The AR-33 test exempts the whole Host. When the first infrastructure adapter project lands (file storage, from
  S06), replace the exemption with an explicit allow-list of adapter assemblies (S01-T1 pre-review).
- Dependency updates are manual (exact pins everywhere): add Dependabot or Renovate for NuGet, npm and GitHub
  Actions, and pin actions by commit SHA (Phase 9 hardening at the latest).
