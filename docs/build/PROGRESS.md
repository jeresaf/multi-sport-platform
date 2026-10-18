# Progress log

Claude Code updates this file at the end of **every** task (keep it short and factual). The reviewer reads it
at the start of every review.

## Now

- **Current slice:** S02 — Tenancy core and isolation (docs/build/slices/S02-tenancy-core.md, as amended by the
  S01 slice review). S00 and S01 are done
- **Current task:** S02-T1a — Tenancy module, tenants: create, read, update (branch `s02/t1a-tenants`; T1 split
  into T1a and T1b, owner 2026-10-18)
- **Next:** S02-T1b — tenant status transitions
- **Blocked on:** nothing

## Next up

1. S02-T1a — Tenancy module, tenants: create, read, update
2. S02-T1b — Tenant status transitions and `TenantStatusChanged.V1`
3. S02-T2 — Isolation enforcement (application + database)

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
| 2026-10-17 | S01-T3c | #18 | One OpenAPI 3.1 document `v1` (no `servers`; `ProblemDetails` with required `code`/`correlationId`), served with Scalar in Development only; `.ProducesErrors(...)` documents error codes per status (`x-error-codes`); `SportsPlatformApp` shared by API and CLI; CLI `export-openapi` → `contracts/openapi.json`; `@msp/api-client` (openapi-typescript + openapi-fetch, `isProblemDetails`); CI job `Contracts` fails on stale generated files | Explicit `UseRouting`/`UseEndpoints` (an unstarted app otherwise documents no endpoints); strict JSON numbers; openapi-typescript's TS ^5 peer allowed for 6. Health probes left out of the document (owner) |
| 2026-10-17 | S01-T3b | #17 | Serilog JSON console logs with `CorrelationId`, `ActorKind`, `ActorId`, `TenantId` (`RequestContextEnricher`) and one completion event per request; OpenTelemetry traces and metrics (ASP.NET Core, HttpClient, Npgsql; OTLP only when configured; probes not traced); `GET /health/ready` (database on `App`, status-only body); `VALIDATION_REQUEST_TOO_LARGE` (413); relaxed escaping in `contracts/errors.json` (#16 R1) | JSON in every environment, no OTLP logs, 413 code with 408/431 documented as body-less (owner). Pre-review: real-Kestrel 413 test, enricher re-entry guard. Review: ASP.NET Core `RequestId` dropped from logs (R2) |
| 2026-10-17 | S01-T4 | #19 | `ActionDefinition` (code, scope, scopable-by, audit, entitlement, idempotent, since), `.RequireAction(...)`/`.AllowPublic()`, `IAuthorizationEvaluator` (default deny → 403 `AUTH_FORBIDDEN`) enforced by a middleware after routing; Host refuses to build with an unmarked, double-marked or public admin/platform endpoint; health, OpenAPI, Scalar and the public group are public; `PlatformActions` (`platform.outbox.read`, `platform.outbox.redrive`); `export-catalogues` → `docs/generated/actions.md`, `contracts/actions.json`; `TestAuthorizationEvaluator` | Middleware instead of endpoint filter and build-time endpoint check (owner). Pre-review: marked non-route endpoints evaluated, protected paths from parsed segments |
| 2026-10-18 | S01-T5a | #20 | Domain event dispatch (`IDomainEventSubscriber<T>`, rounds before commit, `MessagingInterceptor`); `outbox_messages`/`inbox_messages` in every module schema; `ModuleDbContext.Publish` with the ADR-0009 envelope; `OutboxProcessor` (`FOR UPDATE SKIP LOCKED`, strict per-aggregate `sequence` order, backoff 2 s…5 min, `FAILED` after `MaxAttempts`) hosted by Platform; consumers (`IIntegrationEventConsumer<T>`) deduplicated by the inbox under the event's tenant; scoped `TenantContext`/`CorrelationContext`; outbox state machine in state-machines.md | T5 split in two (owner). No RLS on outbox/inbox (owner, Q15). Pre-review: strict per-aggregate ordering (owner) with `sequence` and monotonic outbox IDs (EF orders inserts by key); ~1,700 changed lines without migrations, kept as one PR. Review: per-module backoff on failures (R1); migrations kept as two per context (R2, owner) |
| 2026-10-18 | S01-T5b-1 | #21 | `EventCatalogue` (Host): every `IIntegrationEvent` in the modules' Contracts with payload members (camelCase name, simple type, nullability) and registered consumers; `export-catalogues` → `docs/generated/events.md`, `contracts/events.json` (empty: no events yet), checked by the `Contracts` job | T5b split in two (owner). Export fails on duplicate, badly named or struct events, undescribable payload types, a consumer registered twice or subscribed to an undeclared event (pre-review). No `since` column. Review: payload enums are UPPER_SNAKE strings (R1), `DateTime` members rejected (R2) |
| 2026-10-18 | S01-T5b-2 | #22 | `OutboxDiagnostics` (BuildingBlocks, through each module's DbContext); Platform endpoints `GET /api/platform/v1/diagnostics/outbox` (`module`, `status`, `limit`, `cursor`; newest first across modules; no payload; `heldBackBy`) and `POST …/{module}/{messageId}/redrive` (`reason` 1–500, `FAILED` → `PENDING`, attempts 0); `PLATFORM_OUTBOX_MESSAGE_NOT_FOUND`/`_NOT_FAILED`, `VALIDATION_INVALID_QUERY`/`_REASON_REQUIRED`; HTTP JSON enums as UPPER_SNAKE strings; allowed/denied tests for both outbox actions; T5 acceptance complete | FluentValidation 12.2.0 (Apache-2.0, owner on review) with `.ValidateRequest<T>()` and `errors: [{field, code}]`; `ToProblem(detail)`; `TestAuthorizationEvaluator.Reset` before each database test. ~940 changed lines without generated files, kept as one PR. Review: `limit` parsed by the request, so every bad value is `VALIDATION_INVALID_QUERY` (R1); T6 audits the re-drive in the target module's outbox (R2) |
| 2026-10-18 | S01-T6a | #23 | `audit.audit_entries` (append-only, indexes for T6b's filters); `AuditScope` + `AuditMiddleware`: the first module save commits `AuditRecorded.V1` in that module's outbox (consumer `platform.audit`), denials/failures/no-save actions go directly through `IAuditLog`; error code recorded from Problem Details; `[PersonalData]` + `AuditMetadata` redaction; re-drive audited in the target module's outbox with its reason, the list with its filters | T6 split in two (owner). Defaults approved: no RLS until S02, direct `SUCCESS` for no-save actions, one unit of work per command, validation failures audited. Test reset now covers Platform's `audit` schema. Pre-review: cancelled saves discard their audit messages. Review: reasons are permanent, no personal data (R3); anonymous actor and denial rate limits are follow-ups (R1, R2) |
| 2026-10-18 | S01-T6b | #24 | `GET /api/platform/v1/audit-entries` (`platform.audit.read`, audited with its filters): `tenantId`, `actorId`, `action` (format only), `entityType`/`entityId` (needs the type), `correlationId`, `outcome`, `from` (inclusive)/`to` (exclusive) instants with an offset, at most microseconds; newest first by `(occurred_at, id)` with `AuditCursor`; `ActionCode.IsValid`; T6 complete | `outcome` filter beyond the spec; no module or "no tenant" filter (owner, plan). No new errors, events or migrations. Pre-review: read's own entry has no target tenant (S02 follow-up), instants limited to microseconds |
| 2026-10-18 | S01-T7a | #25 | Seed framework (`SportsPlatform.BuildingBlocks.Persistence.Seeding`): `ISeeder` (category, order, checksum), `SeedRunner` (prerequisite categories first, unchanged seeders skipped, a failure records nothing), `SeedEnvironmentPolicy`, owner contexts on `Migrator` for REFERENCE/CONFIGURATION only; `DeterministicIds` (UUIDv5); `platform.seed_runs` (append-only, Platform's `SeedRunLog`); `DatabaseReset` (drop every module schema, migrate); CLI `seed --category <c> [--reset]`; test-only seeders | T7 split in two (owner). DEMO/TEST also in Testing, reset by dropping schemas (owner, plan). No error codes: refusals are a `SeedReport`. Pre-review: the reset checks Development itself and never drops system schemas, keys and checksums checked before a seeder runs, derived test hosts must use the container's connections. ~1,250 changed lines without migrations, kept as one PR. Review: reset only of a local database or one named in `Seeding:AllowResetOf` (R1), advisory lock for one run at a time (R2), tenant and audit for DEMO seeders a follow-up (R3) |
| 2026-10-18 | S01-T7b-1 | #26 | `platform.countries` (249), `currencies` (155, money only, minor units), `languages` (183, all ISO 639-1), `time_zones` (419, `zone.tab` + `Etc/UTC`): read-only, unique codes with format checks, `is_active`; data files embedded in Platform (`Seed/Reference/Data`) from CLDR 48, ISO 4217 (SIX), LoC ISO 639-2 and tzdata 2026c, regenerated by `backend/tools/reference-data/build-reference-data.cs`; four REFERENCE seeders on one `ReferenceSeeder<TItem, TEntry>` (upsert by code, deactivate, reactivate); tests seed the real data once and the reset keeps it | T7b split in two, CLDR/ISO/IANA sources, cache headers only (owner, plan). `XK` left out (Q16). Unit tests reference Platform. No actions, errors or events. Pre-review: alpha-3 and numeric codes unique among active rows only. Review: deactivations saved before inserts (R1), no funds, metals or units (R2, owner), display names for CD, CG, HK, MM, MO (R3, owner) |
| 2026-10-18 | S01-T7b-2 | #27 | `GET /api/public/v1/reference/{countries|currencies|languages|time-zones}` (Platform `Features/Reference`): `{ items }`, active entries only, ordered by `code` (ordinal); `time-zones?country=` (upper-case alpha-2, else `VALIDATION_INVALID_QUERY`; no zones → `[]`); `CacheableJson` (BuildingBlocks `Api`): `Cache-Control: public, max-age=3600`, strong SHA-256 `ETag`, 304 on a matching `If-None-Match` (weak comparison, lists, `*`); OpenAPI (200/304) and TS client; T7 complete | Owner defaults (plan): sort by code, strict `country`, `code` for the IANA ID, no HEAD; no server cache, no actions, new errors, events or migrations. Pre-review: cacheable responses must be the same for every caller (no `Set-Cookie`), the edge strips the replayed `X-Correlation-Id` (api-conventions.md) |
| 2026-10-18 | S01 acceptance | — | Slice acceptance checked on `main` (all five criteria met); spec boxes ticked; tech debt regrouped by owner (S02, S03, infrastructure after M1, later) | Local DB had a pre-merge `AddReferenceData` migration applied; rebuilt with `seed --reset` (local only, no code change) |
| 2026-10-18 | S01 slice review (reviewer, 2026-10-18) | — | S01 accepted; roadmap S00 and S01 → ☑; S02 spec amended (tasks reordered so RLS lands before the first tenant-owned business table, decisions D1–D10, S01 carry-overs assigned to tasks); owner decisions recorded; CLI launch profile documented; modules.md names `AuditScope` and the audit middleware | Owner's 30-minute contributor check done 2026-10-18 |

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
  metrics (not logs) over OTLP. Owner approved the plan 2026-10-17. T3c (plan approved 2026-10-17): health probes
  are not in the OpenAPI document; `ProducesErrors` added now (not with T7). Found while implementing, for the
  reviewer and owner to confirm on #18: the Host calls `UseRouting`/`UseEndpoints` explicitly (routing now runs
  after the correlation, exception and status-code middleware), and JSON numbers are strict (`"5"` → 400
  `VALIDATION_MALFORMED_REQUEST`).
- **S01-T4:** authorization is enforced by a middleware after routing, not an endpoint filter (docs/architecture/
  authorization.md updated): it covers every endpoint type and denies before the body is bound. Beyond the spec,
  the Host also checks the endpoint rules when it is built (not only in a test). Owner approved the plan 2026-10-17.
- **S01-T5:** delivered as two PRs (T5a dispatch, outbox, processor, inbox; T5b event catalogue, diagnostics and
  re-drive with "re-drive succeeds after fix"). Outbox statuses added to docs/domain/state-machines.md; outbox and
  inbox in every module schema, read-write, nullable `tenant_id`, no RLS (Q15, answered 2026-10-18). Owner approved the plan 2026-10-17.
  On the pre-review (2026-10-18) the owner chose strict per-aggregate ordering: a retrying or `FAILED` message holds
  back later messages of its aggregate. The PR is well over the ~600-line guideline; kept as one PR.
  T5b is delivered as two PRs (T5b-1 event catalogue; T5b-2 diagnostics and re-drive). Owner approved the plan
  2026-10-18, with its defaults: re-drive is one message at a time, takes a required reason that T6's audit records,
  and the diagnostics list leaves the payload out. T5b-2 (plan approved 2026-10-18): `VALIDATION_INVALID_QUERY` and
  `VALIDATION_REASON_REQUIRED` live in `CommonErrors` so later endpoints reuse them; no `status` means all statuses.
  On the review (#22) the owner chose to add FluentValidation 12.2.0 (Apache-2.0) now: `.ValidateRequest<TRequest>(error)`
  runs the module's `IValidator<TRequest>`, and validation problems carry `errors: [{field, code}]`.
- **S01-T7:** delivered as three PRs (T7a seed framework; T7b-1 reference tables and seeders; T7b-2 public endpoints). Owner approved the plan
  2026-10-18 with its defaults: `DEMO`/`TEST` also run in the `Testing` environment (the spec names Development and
  Staging); a category runs `REFERENCE` and `CONFIGURATION` first; `--reset` drops every module schema as the owner and
  migrates again (not a database re-create, which needs the superuser); languages are all of ISO 639-1 (T7b).
  T7b (plan approved 2026-10-18): data from Unicode CLDR 48 (names, country alpha-3/numeric), ISO 4217 list one,
  the Library of Congress ISO 639 table and IANA tzdata 2026c; the public endpoints are cacheable through HTTP headers
  only (`Cache-Control` + ETag), with no server-side cache. T7b-2 (plan approved 2026-10-18): lists sorted by `code`,
  `country` strictly upper-case alpha-2, the time zone's IANA ID named `code`, no HEAD.
- **S01-T6:** delivered as two PRs (T6a audit trail; T6b query endpoint). The spec's "command pipeline" didn't exist:
  T6a adds `AuditScope` and the Host's `AuditMiddleware` instead of a pipeline. Owner approved the plan 2026-10-18 with
  its defaults: `tenant_id` nullable without RLS until S02; audited actions that save nothing (reads) are written
  directly as `SUCCESS` (the spec names only denials and failures); one unit of work per command (an explicit
  transaction rolled back after its save loses the entry); validation failures are audited as `FAILURE` with their code.
  T6b (plan approved 2026-10-18): an `outcome` filter beyond the spec's list (pack spec 40's Audit Explorer); no module
  filter (no column) and no "entries without a tenant" filter until S02; `entityId` needs `entityType`.

## Tech debt / follow-ups

Consolidated by the S00 slice review (2026-10-17); regrouped by owner (slice or trigger) at S01 acceptance
(2026-10-18). The S00 carry-overs assigned to S01 are done. Each item names where it gets done.

**S02 — tenancy**
- **S02-T2:** `ITenantOwned.TenantId` stays get-only: S02's interceptor sets it with `entry.Property(...).CurrentValue`, never
  a public setter. Moving `BuildingBlocks/Modules` to a `BuildingBlocks.Web` project is optional, not planned
  (reviewer guidance on #13).
- **S02-T2:** `audit.audit_entries` has a nullable `tenant_id` and no RLS: S02 adds the policy (tenant users see their tenant's
  entries; entries with no tenant only through platform services) and the isolation tests, and revisits the audit
  indexes (tenant first) with the policy (S01-T6a pre-review); then also make the entity index
  `(entity_type, entity_id, occurred_at)` so an entity's history needs no sort (#24 R2).
- **S02-T5:** DEMO/TEST seeders run application commands outside HTTP (#25 R3): before the first DEMO seeder (S02 or later),
  `SeedContext` gets a way to run commands as a tenant (set `TenantContext` in the seeder's scope, needed once S02's
  RLS is on) and begins an audit scope so demo data is audited with source `SYSTEM` (reviewer's suggestion; owner: yes,
  2026-10-18).
- **S02-T2:** Platform reads of one tenant's data are audited with `tenant_id` NULL: `AuditScope` takes the tenant from
  `ITenantContext`, empty on platform routes, so `GET /api/platform/v1/audit-entries?tenantId=…` stores the tenant only
  in metadata. multi-tenancy.md says platform calls are audited with the target tenant: S02 adds a target tenant to
  `AuditScope.Describe` (set when a platform read is filtered to one tenant) with the audit RLS (S01-T6b pre-review).
- **S02-T1:** `IReferenceData` (modules.md, Platform Contracts) doesn't exist yet: add it with its first consumer (S02 tenant
  settings validating time zone, currency, locale) (S01-T7b-1).
- **S02-T1:** `WithErrorCode` may only take a declared `Error`'s code (`XErrors.Y.Code`), otherwise the error catalogue misses it:
  add this rule to `.claude/rules/backend.md`. With the next validator that sets a code (#22 re-review N3).

**S03 — identity and access**
- Anonymous actor for audit (#23 R1): `actor_kind` allows `USER`/`SYSTEM` and `actor_id` is NOT NULL, so once S03
  has real sign-in an anonymous 401 on an audited endpoint would fail the direct insert and the denial would only be
  logged. S03: add an anonymous actor kind (nullable or fixed `actor_id`) with the check-constraint migration, and a test
  that an anonymous 401 on an audited endpoint writes a `DENIED` entry.
- Denials cost a permanent row each (#23 R2): rate-limit `/api/admin` and `/api/platform` at the edge (Cloudflare),
  with the `/health/*` item below (infrastructure task after M1). S03 decides whether anonymous denials are counted per
  minute instead of one row each.
- 401 still produces Problem Details without a `code` (no authentication yet); comes with S03. 403 from the
  authorization middleware is `AUTH_FORBIDDEN` (T4) (S01-T3a pre-review).
- `ICurrentActor` is still the singleton `SystemActor`, so every HTTP request (anonymous ones included) is logged as
  `ActorKind=System`. Add a per-request actor with an anonymous kind for unauthenticated requests and update
  `RequestLoggingTests.Request_is_logged_with_correlation_ID_and_actor` (S03, or T4 if it needs one; #17 R1).

**Infrastructure task after M1**
- A failed direct audit write (denial, failure, read) is only logged: the response is already written. Alert on that
  Error with the `FAILED` outbox alert below (infrastructure task after M1; S01-T6a).
- Alert on any `FAILED` outbox message (OpenTelemetry counter or gauge): with strict ordering one holds back its
  aggregate's later events until re-driven (infrastructure task after M1; #20 R3).
- `/health/ready` is anonymous and runs `SELECT 1` on every hit: expose `/health/*` only to the load balancer and
  probes, or rate-limit them at the edge (infrastructure task after M1, ADR-0016; #17 R3).
- The public reference lists (S01-T7b-2) query the database and hash the body on every request (no server cache,
  owner): rate-limit `/api/public` at the edge and let the CDN honour `Cache-Control`/`ETag` (infrastructure task
  after M1, with the `/health/*` item above).

**Owner decisions / housekeeping**
- Merge style: README-FIRST says squash-and-merge; S00 used merge commits. Pick one. For stacked PRs, retarget
  the next PR to `main` before merging it.
- The prototype's old branches (`agent/*`, `codex/*`, `feature/*`, `ci/*`) are not all covered by tag
  `prototype-v0`; keep them or tag them before deleting. (The merged S00 branches and `restart/kit` were deleted
  on 2026-10-17.)
- No GitHub Pro for now (owner, 2026-10-18): `main` stays without branch protection; merge only on green CI.
- Country `PS` keeps the CLDR name (owner, 2026-10-18).
- Demo seeding is audited (owner, 2026-10-18): `SeedContext` runs commands as a tenant with source `SYSTEM` (S02-T5).

**Later** (each item names its trigger)
- `xmin`/`updated_*` cover only the aggregate root's row: a change to only a child entity or owned type doesn't
  bump the root's version, so two edits to different children both succeed. Mark the root `Modified` when a
  dependent changes, with a test, before the first aggregate with child tables (S06 at the latest) (S01-T2a
  pre-review).
- The audit middleware's exception path (`FAILURE` with `PLATFORM_UNEXPECTED_ERROR`) has no test: no audited endpoint
  can throw yet. Add one with the first audited endpoint that can (S01-T6a).
- Audit retention and partitioning by `occurred_at` (pack spec 40 §7): with the retention job (S01-T6a).
- `platform.outbox.read` (and T6b's `platform.audit.read`) write an entry per page: diagnostics and audit screens must
  not auto-refresh by polling (#23 review).
- `GET …/diagnostics/outbox?status=…` orders by `id` and the only status index is `(status, next_attempt_at,
  sequence)`: with a large `PROCESSED` history PostgreSQL sorts or walks the primary key. Add an index on `(status, id)`
  if the page gets slow, at the latest with the retention job (S01-T5b-2 pre-review).
- Outbox and inbox tables grow forever: add a retention job for `PROCESSED` messages and old inbox rows (Hangfire,
  first slice that adds scheduled jobs; S01-T5a).
- Human-readable console logs in Development (owner, #17 re-review): a readable console formatter behind a
  setting, used by the Development launch profile; JSON stays everywhere else. Can ride the next PR.
- A missing `IValidator<T>` for an endpoint with `.ValidateRequest<T>()` only fails at the first request (500 from
  `GetRequiredService`): have the Host check this at startup like action markers, or add a test that walks the
  endpoints. With the next endpoint that has a validator (#22 re-review N1).
- Validation field names are camelCased by their first letter only (`JsonNamingPolicy.CamelCase.ConvertName` on the
  whole path), so `Address.Street` becomes `address.Street`: convert each path segment. With the first nested request
  body (#22 re-review N2).
- `ProblemDetails:TypeBaseUri` is the placeholder `https://errors.sportsplatform.invalid/` until Q10 (domain).
- Reference names are English only: CLDR has Swahili, French and other names for the same codes; add a localised
  name per language with the first non-English UI (S01-T7b-1).
- The OpenAPI document gives `time-zones?country=` no `pattern` (`^[A-Z]{2}$`) and documents no `ETag`/`Cache-Control`
  response headers on the reference lists (the endpoint descriptions say both): add them with the first web consumer of
  the reference lists, or when the OpenAPI document gets header support (#27 R1).
- tzdata releases several times a year: rerun `build-reference-data.cs` with a new `Tzdata` pin when one changes a
  zone the customers use (the API host's own tzdata, used by `TimeZoneInfo`, comes with its container image).
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
