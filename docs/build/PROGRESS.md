# Progress log

Claude Code updates this file at the end of **every** task (keep it short and factual). The reviewer reads it
at the start of every review.

## Now

- **Current slice:** S03 — Identity and access (docs/build/slices/S03-identity-access.md, rewritten by the S02 slice
  review). S00, S01 and S02 are done
- **Current task:** S03-T4a — Memberships, role assignments, the identity directory and the evaluator's tenant step
- **Next:** S03-T4b — Member and role endpoints, last owner, access-mode refinements
- **Blocked on:** nothing

## Next up

1. S03-T4 — Memberships, invitations and tenant roles (three PRs, owner: T4a data, directory and evaluator; T4b
   member and role endpoints, last owner, access mode; T4c invitations, email, DEMO)
2. S03-T5 — Capabilities and the authorization matrix
3. S03-T6 — Admin web: sign-in, shell, members, platform console (likely two PRs: T6a, T6b)

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
| 2026-10-18 | S02-T1a | #30 | New module `Tenancy` (schema `tenancy`): `tenants` (slug unique for ever, "C" collation, format checks) and append-only `tenant_status_history`; `Tenant` aggregate with the full state machine; `POST`/`GET`/`GET {id}`/`PATCH {id}` under `/api/platform/v1/tenants` (`tenant.create`, `tenant.read`, `tenant.update`, audited under the target tenant, D2); `TenantCreated.V1`; `IReferenceData` in Platform Contracts; `VALIDATION_INVALID_REQUEST`; `WithErrorCode` rule in backend.md | T1 split in two; locale, version, history and `publicEnabled` defaults (owner, plan). Status transitions over HTTP come with T1b |
| 2026-10-18 | S02-T1b | #31 | `POST /api/platform/v1/tenants/{id}/{activate\|suspend\|restore\|archive\|unarchive}` (`tenant.activate`, `tenant.suspend`, `tenant.restore`, `tenant.archive`, `tenant.unarchive`; reason 1–500 for suspend, archive, unarchive) through one `ChangeTenantStatusHandler`; `TenantStatusChanged.V1`; `IfMatch` (BuildingBlocks `Api`) on PATCH and the transitions; slug check before the tenant is set; tests for both T1a races; T1 complete | Owner on #30: R1 evaluate `If-Match`, R2 amend D1. No migrations, new errors or dependencies. Pre-review found `ValidateRequest` threw (500) when a required body was missing (filters run with a null argument): now 400 `VALIDATION_MALFORMED_REQUEST`, also for the outbox re-drive |
| 2026-10-18 | S02-T2a | #32 | Tenant isolation in EF and PostgreSQL: named query filter `TenantIsolation` on every `ITenantOwned`; `TenantOwnershipInterceptor` stamps `TenantId` through the change tracker (#13) and throws on no/another tenant; `TenantConnectionInterceptor` sets `app.tenant_id` on every open; `EnableTenantRls`/`DisableTenantRls`; composite FKs required between tenant-owned entities (checked at model build); `TenantContext.Set` once per scope (D1); `tenant_status_history` tenant-owned with RLS; isolation harness (`IIsolationSample` per entity), pool leak, consumers under RLS, D1 timing tests | T2 split in two and composite FKs enforced (owner). No new actions, errors, events or dependencies |
| 2026-10-19 | S02-T2b | #33 | Audit under RLS: `audit.audit_entries` gets `EnableTenantRls`; `AddPlatformServiceDbContext` (connection `Platform`, not migrated) and `PlatformAuditDbContext` for `AuditLog`, `AuditRecordedConsumer` (inbox row in the same transaction) and the platform audit query (D3); tenant-first action/actor indexes and `(entity_type, entity_id, occurred_at)` (#24 R2); `AuditScope.Describe(targetTenantId:)` (audit read with `?tenantId=` audited under that tenant); RLS coverage test (forced RLS, one permissive `tenant_isolation` policy, exceptions with reasons); audit-under-RLS tests; architecture tests for `IgnoreQueryFilters` and the audit context's users; T2 complete | Only `*.outbox_messages` is an exception (`inbox_messages` has no `tenant_id`). No new actions, errors, events or dependencies |
| 2026-10-19 | S02-T3a | #34 | Admin and platform tenant resolution: pipeline routing → actor → audit → tenant resolution → authorization (D8). `X-Tenant-Id` with membership (`ITenantMembershipChecker`, Host default none) for tenant actions → `AUTH_TENANT_FORBIDDEN`; `.RunsUnderRouteTenant()` on `/tenants/{tenantId}` routes → `TENANT_NOT_FOUND`, handlers no longer set the tenant; access-mode step 1 (`ITenantAccess`, status-only) → `TENANT_BLOCKED`. Scoped `ActorContext` from the principal; test authentication scheme replaces `TestActor`; `TestTenantContext` retired. `ITenantDirectory` (ID, slug). AR-21 test | Owner (Q17): T3 split into T3a/T3b and `tenant_domains` moved from T4 to T3b (slice spec updated). Errors stay in `TenancyErrors`; Contracts return `Result`s carrying them, so the Host uses Contracts only. Seed owner contexts stamp the system actor (singleton factory can't take the scoped actor). `SportsPlatformApp` split into `AddServices`/`UsePipeline` so `AdminTestApp` reuses the pipeline |
| 2026-10-19 | S02-T3b | #35 | Public tenant resolution: `tenancy.tenant_domains` (global; platform subdomain `<slug>.<Platform:BaseDomain>` created with each tenant, backfilled by the new post-migration step), `.RequirePublicTenant()` endpoints resolve the host through `ITenantDirectory.GetByHostAsync` and a 60 s cache cleared on changes → `TENANT_NOT_FOUND` / `TENANT_UNAVAILABLE`; trusted `X-Forwarded-Host` (configured proxies only); Development override `X-Tenant-Slug` / `?tenant=`; `GET /api/public/v1/site` | Owner: tenant domain state machine added; `tenant_domains` is an RLS coverage exception; backfill as a post-migration step, not SQL. Directory slug/host lookups return `Result`s. No proxies configured → forwarding off (framework trusts everyone with empty lists). `/site` max-age 1 min (plan said 5: kept no longer than the host cache). T2b follow-up (platform routes on a tenant host) done. #35 R1: a `localhost` base domain is refused outside Development and Testing |
| 2026-10-19 | S02-T4a | #36 | `tenancy.tenant_settings` (first tenant-owned business table, RLS, one row per tenant created with it): time zone, locale, currency, `publicEnabled`, `starting_soon_minutes` (30, 5–180), `minor_registration_consent` (`RECOMMENDED`); migration copies existing tenants' values before RLS and drops the four `tenants` columns; platform create/PATCH write the settings row, detail and transitions return it, the list (`TenantListItem`) doesn't; public resolution sets the tenant, then `CheckPublicAccessAsync` reads `publicEnabled` under RLS (cached with the host); `TenantSummary` lost `PublicEnabled`; isolation sample, RLS coverage, migration down/up test | Owner: all six settings only in `tenant_settings`, T4 split in two, platform PATCH version guards the tenant row only. No new actions, errors, events or dependencies. Pre-review: settings conflict test, status-change response read before the save, Down drops temporary defaults; ETag over settings a T4b follow-up |
| 2026-10-19 | S02-T4b | #37 | `GET`/`PATCH /api/admin/v1/tenant/settings` (`tenant.settings.read`, `tenant.settings.update`, TENANT, audit `REQUIRED`) for the tenant in `X-Tenant-Id`; all six keys, null = unchanged; settings row's own `version`/`ETag`; host cache cleared when `publicEnabled` changes; BuildingBlocks `StrictJsonBody` names unknown, wrongly typed or repeated keys (`VALIDATION_INVALID_REQUEST`); Host documents pipeline 403s in OpenAPI; platform tenant `ETag` documented as the tenant row's only | Owner: field-named errors on the settings PATCH only; enum values read case-insensitively (existing converter). Beyond the plan: `AUTH_FORBIDDEN` documented on every admin/platform endpoint. No new errors, events, migrations or dependencies. Pre-review: member checks honour property converters, required members refused, empty body and malformed `If-Match` tests. #37 R1: test tenants renamed to invented bodies listed in pilot-customer.md ("Other tenants (tests only)") |
| 2026-10-19 | S02-T5a | #38 | `SeedContext.RunCommandAsync` (DEMO/TEST: a new scope per command, tenant set once, audit begun with source `SYSTEM`; a command that saves nothing throws) and `QueryAsTenantAsync` (reads under a tenant, unaudited); `CreateTenantHandler`/`Tenant.Create` take an optional ID for seeds; DEMO seeder `tenancy.demo.tenants`: KMSA and ELSSC through create → activate → settings update, deterministic IDs, idempotent by slug | Owner (plan): T5 split in two; a scope per command; consent `REQUIRED` and `publicEnabled` beyond the spec's fields. A tenant in another status than `PROVISIONING` keeps it; a changed profile is left alone. Audit entries reach `audit_entries` when the API's outbox processor runs (the CLI only queues them). No new actions, errors, events, migrations or dependencies. Pre-review: create runs without a tenant (#30 R2). #38 N1: the seeded-ID create overload is internal |
| 2026-10-19 | S02-T5b | #39 | Public app resolves its tenant per request (`lib/site.ts`: host without port → `/site` as `X-Forwarded-Host`, `MSP_API_URL`); `(site)` layout with the tenant's name and footer "<name> · Runs on MSP", "Coming soon"; not-found page (404, unknown host or path), unavailable page (200, `noindex`), API failure thrown (500, digest shown, correlation ID logged); Vitest for the lookup; `pnpm e2e` runs against the real API (`web/e2e/start-api.sh`: build, migrate, seed DEMO, Host in Development) and covers both tenants by host and an unknown host; CI e2e job starts PostgreSQL and .NET and uploads phone/tablet/desktop screenshots, light and dark | Unavailable page 200 + `noindex`, footer wording, screenshots as a CI artifact (owner). Pre-review: API failures are 500s, not-found wording fits unknown paths |
| 2026-10-19 | S02 slice review | #40 | Docs only: S03 spec rewritten by the reviewer (D1–D18, T1–T6); S02 slice acceptance ticked and slice review note; roadmap S02 ☑; tech debt regrouped (first deployment section, S03 carry-overs with their tasks); open questions Q18 (bearer tokens) and Q19 (multi-factor sign-in) | All S02 acceptance items met on main (109b9c9); owner demo done 2026-10-19 |
| 2026-10-19 | S03-T1a | #41 | New module `Identity` (schema `identity`): ASP.NET Core Identity core with `UserOnlyStore` (no roles tables, store never saves by itself), `users` (+ `user_claims`, `user_logins`, `user_tokens`; unique `normalized_email`; contacts tagged `pii` `PRIVATE`), D15 password/lockout options; `platform_role_assignments` shipped early; data protection key ring in `identity.data_protection_keys` (append-only, application name `msp`); CLI `create-platform-admin --email` (password from `MSP_ADMIN_PASSWORD`, audited `platform.admin.create` as `SYSTEM`, prints the ID not the email); CLI applies the Host's `Serilog:MinimumLevel` (no EF SQL); default-credential scan test | T1 split in two; role table early; CLI action audited; existing user refused (owner, plan). `User` lives in `Infrastructure/Users` (Identity's base class is ASP.NET Core, which `Domain/` may not use). Errors beyond the plan: `AUTH_CONCURRENCY_CONFLICT` (every module context needs one), `AUTH_PASSWORD_REJECTED` (T2's, early), `AUTH_USER_EXISTS`. Refusals are not audited (operator console). New packages (MIT): `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.13 |
| 2026-10-19 | S03-T1b | #42 | `IEmailSender` + `EmailMessage` (recipient `PRIVATE`) in Platform Contracts; MailKit `SmtpEmailSender` (one connection per email, logs only the Message-ID; transport failures become `EmailDeliveryException` without the server's text or inner exception, so no address reaches the outbox log or `last_error`); `Email:From`/`FromName`/`Smtp:*` validated on start (credentials over `Security=None` refused); Development and Testing → Mailpit; `TestApp.Emails` records instead of sending; adapter tested against a Mailpit container (accepts one recipient, rejects others) | Fail fast on missing `Email:*` (owner, plan): test hosts started as Staging/Production get mail settings. `System.Security.Cryptography.Pkcs` pinned to 10.0.13 (MimeKit's 10.0.0 has advisories). Compose image lookup moved to `IntegrationTests/Infrastructure/ComposeFile`. New packages (MIT): `MailKit` 4.18.1, with `MimeKit` 4.18.1 and `BouncyCastle.Cryptography` 2.7.0 |
| 2026-10-19 | S03-T2a | #43 | Account family `/api/account/v1` (`ACCOUNT` actions, `allowsAnonymous`, no-store, startup rules); `AnonymousActor` for requests without a user (a principal without a GUID `sub` too) with `actor_kind` widened in audit and tenancy; `401 AUTH_UNAUTHENTICATED` before any tenant lookup and in authorization; CSRF middleware (`X-CSRF: 1`, `403 AUTH_CSRF_REQUIRED`, audited); Identity's cookie as the only scheme (`__Host-msp_session`, 12 h sliding, 7 d absolute, stamp every 5 min, app clock; claims `sub` + stamp); `login` (lockout, same answer and same hashing for every refusal, DENIED entry saved with the count via `AuditScope.RecordOnSave`, race retried), `logout`, `me`; `IdentityAuthorizationEvaluator` replaces deny-all (account step); startup check for missing validators; test scheme falls back to the cookie, test clients https + `X-CSRF` | T2 split in T2a/T2b; Q18 answered (cookies only); T2b rate limits, reset token and `AUTH_CURRENT_PASSWORD_INVALID` agreed (owner, plan). `AUTH_UNAUTHENTICATED`/`AUTH_CSRF_REQUIRED` in BuildingBlocks `AuthorizationErrors` (pipeline errors). Session cookie without `Expires`. No new packages |
| 2026-10-19 | S03-T2b | #44 | `account.password.forgot` (always 202; `PasswordResetRequested.V1` through the outbox, consumer `identity.password_reset_email` creates the token and sends the link to `{Identity:AdminAppUrl}/reset-password`), `account.password.reset` (token `{userId}.{secret}`, SHA-256 in `identity.user_tokens`, 1 h, once, newest wins; signs out every session, clears the lockout), `account.password.change` (wrong current password = `400 AUTH_CURRENT_PASSWORD_INVALID`, counted toward the lockout; other sessions end, this cookie renewed); rate limits on login and forgot-password (per IP before the audit, per normalised email after validation; `429 AUTH_RATE_LIMITED` + `Retry-After`, audited DENIED); `X-Forwarded-For` from trusted proxies; `Identity:*` validated on start; fixes: `UserStore.UpdateAsync` keeps the loaded concurrency stamp, `Identity.TwoFactorRememberMe` registered so a stale stamp signs out instead of 500 | Owner (plan): one PR; change failures lock; reset unlocks; user ID in the token; per-IP 429s not audited; `X-Forwarded-For` trusted (S02 D7 widened). `AUTH_PASSWORD_REJECTED`: rule in `detail`, field in `errors`. No new packages |
| 2026-10-20 | S03-T3 | #45 | `IdentityRoles` (Identity `Domain/Roles`): `PLATFORM_ADMIN` (every platform action), `PLATFORM_SUPPORT` (platform `*.read`), the nine tenant roles with their assignment scopes (tenant-wide roles read the settings; owner and admin change them; `TEAM_MANAGER` nothing yet); `RoleCatalogue` singleton; Host startup check `RoleRules` (undeclared action, wrong scope, action none of a tenant role's scopes can grant, duplicate codes, platform roles ≠ `PlatformRoles`); `export-catalogues` writes `docs/generated/roles.md` and `contracts/roles.json`; `IdentityAuthorizationEvaluator` scoped, platform actions from unrevoked platform roles per request, tenant actions still refused; `AuthorizationRequest` gains `TenantId` and `ScopeRef`; `TestApp` `Authorization.UseIdentityRoles()` | Owner (plan): one PR; all nine tenant roles; settings read for every tenant-wide role; `TEAM_MANAGER` without settings and the scope rule in the startup check. Fixed on the way: `PasswordResetTests` compared two audit entries in ID order, which the fake clock makes random. No migrations, actions, errors, events or packages |

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
- **S02-T1:** delivered as two PRs (T1a module, create/read/update; T1b status transitions). Owner decisions
  2026-10-18: `default_locale` is `ll` or `ll-CC` (language and country checked through `IReferenceData`); `version`
  is optional on changes (stale → `TENANT_CONCURRENCY_CONFLICT`), responses carry `version` and an `ETag`; creation
  writes a status-history row with `from_status` NULL; `publicEnabled` defaults to false; a new shared
  `VALIDATION_INVALID_REQUEST` (400) for body fields (the spec named only existing `VALIDATION_*` codes). Beyond the
  spec: tenant request bodies reject unknown members (`VALIDATION_MALFORMED_REQUEST`), the slug column uses the "C"
  collation (list ordered by slug), and the profile PATCH audit records the names of the fields sent.
  T1b (owner on #30): `If-Match` is evaluated on PATCH and the transitions (R1); tenant creation checks the slug before
  setting the new tenant, so a taken slug is audited without a tenant, and D1 now reads "before the first connection
  that reads or writes tenant data" (R2, spec amended). The race path still audits the would-be tenant ID (documented).
- **S02-T2:** delivered as two PRs (owner, 2026-10-18): T2a enforcement and harness; T2b audit on the `Platform`
  connection, audit RLS and indexes, `AuditScope` target tenant, RLS coverage test, audit under RLS. Every foreign key
  between two tenant-owned tables is composite `(tenant_id, x_id) → (tenant_id, id)`, refused otherwise at model
  build and proven by the harness (owner; multi-tenancy.md said "where catastrophic"). Beyond the spec, the harness
  also proves a row can't be copied into another tenant (RLS `WITH CHECK`); append-only tables refuse cross-tenant
  `UPDATE`/`DELETE` by grant before RLS.
  T2b (plan approved 2026-10-19): the RLS exception list has only `*.outbox_messages` (D4 also named `inbox_messages`,
  which has no `tenant_id`); platform-wide audit filters by action or actor alone use the `occurred_at` index (the
  action and actor indexes are tenant-first); the coverage test also refuses a second permissive policy.
- **S02-T4** (owner, 2026-10-19): two PRs (T4a table and move, T4b admin endpoints). The spec put time zone, locale,
  currency and public visibility in `tenant_settings` while T1 had built them on `tenants`: they move to
  `tenant_settings`, the only home of all six settings; the platform PATCH keeps accepting the four moved fields and
  its version guards the tenant row only; the platform list no longer returns them. `starting_soon_minutes` default 30,
  range 5–180.
  T4b (plan approved 2026-10-19): unknown or wrongly typed keys on the settings PATCH are `VALIDATION_INVALID_REQUEST`
  with field-named `errors` through `StrictJsonBody`; platform tenant bodies keep `VALIDATION_MALFORMED_REQUEST` for
  unknown members. The platform tenant `ETag` stays the tenant row's (documented), not derived from both versions.
- **S02-T3b** (owner, 2026-10-19): `tenancy.tenant_domains` is a second RLS coverage exception (extends D4); its
  `status` follows a new tenant domain state machine in state-machines.md; existing tenants are backfilled by a
  post-migration step, not by SQL in the migration. In the plan the owner approved: `/site` has no "public flags" (a
  200 always means visible; T4 adds public visibility settings).
- **S03-T1** (owner, 2026-10-19): two PRs (T1a Identity module, users, key ring, `create-platform-admin`; T1b email
  transport). `identity.platform_role_assignments` ships in T1a so the command is complete; the command is audited as
  the new CLI-only action `platform.admin.create`; an email that any user already has is refused (no role granted to
  an existing user).
- **S02-T5:** two PRs (owner, 2026-10-19): T5a seeding and DEMO tenants, T5b public web shell and e2e against the real
  API. `SeedContext` runs each command in its own scope rather than "once in the seeder's scope" (D1 and `AuditScope`
  allow one tenant and one action per scope). The demo tenants also get `minor_registration_consent = REQUIRED`
  (pilot-customer.md), `publicEnabled` and locale `en-UG`.

## Tech debt / follow-ups

Consolidated by the S00 slice review (2026-10-17); regrouped by owner (slice or trigger) at S01 acceptance
(2026-10-18). The S00 carry-overs assigned to S01 are done. Each item names where it gets done.

**S03 — identity and access**
- Role assignment changes touch only `identity.role_assignments` rows, so the membership's `xmin` doesn't change and
  two concurrent changes to one membership's roles aren't detected (database.md "Known gap"). T4b's assign and revoke
  handlers must change the membership row too (or lock it), and the last-owner rule takes its per-tenant lock (S03-T4a).
  (T4b)
- Tests that order audit entries by ID (`AuditTestData.EntriesAsync`) see entries written at one fake-clock instant
  in random order (UUIDv7 from `AuditScope`'s clock); compare such entries without order, or order by something
  stable (S03-T3, found in `PasswordResetTests`). (Any task touching audit tests)
- Denials cost a permanent row each (#23 R2): rate-limit `/api/admin`, `/api/platform` and `/api/account` at the edge
  (Cloudflare), with the `/health/*` item below (infrastructure task after M1). Decided (S03 D3): anonymous denials
  keep one row each; login and forgot-password are also rate-limited in the API (done, S03-T2b: in memory, per API
  instance, so N instances allow N times the limit until the edge limits exist).
- `PROVISIONING` is `FULL` for every member in S02-T3a (no roles yet); state-machines.md gives it only to tenant
  owners/admins (setup). S03: limit it to them, with a test (#34 R1).
- `BLOCKED` access mode lets the tenant owner view the tenant's status (state-machines.md); S02-T3a refuses every
  admin request on a blocked tenant because there are no roles yet. S03: the exception for the owner (S02-T3a).
- **OpenAPI (with the first admin UI client, S03 or later):** optional request members (`version`, PATCH fields) are
  listed as `required` (nullable) in the schemas, so the TS client makes callers pass `null`; endpoints don't declare
  the `If-Match` request header or the `ETag` response header, and activate/restore list no 400 (pipeline errors).
  Fix with defaults on the request records or a schema/operation transformer (S02-T1b pre-review). The admin settings
  PATCH (`UpdateTenantSettingsRequest`) has the same `required` problem; strict-body requests are components named by
  short type name, so two such types with one name would share a schema (S02-T4b pre-review). (T6)
- Admin web sign-out (#43 N2): `POST /api/account/v1/logout` needs a session, so an idle-expired one answers 401; treat
  401 from logout as "signed out". Audit screens (#43 N1): a successful sign-in is audited with actor `ANONYMOUS` and
  entity = the user; if the screens filter by actor, consider recording it as the user. (T6)
- shadcn/ui is not initialised yet (ADR-0011); do it with the first real admin UI (S03). `Button` is hand-written. (T6)
- Logout deletes the cookie only (no server-side session store): a copied cookie lives until it expires or the
  security stamp changes. Identity's lockout uses the system clock (`UserManager` takes no `TimeProvider`). (Later,
  S03-T2a)
- Per-IP rate-limit key behind the real proxy chain (#44 M2): it is per client only if every hop is trusted and forwards
  the client's address (`ForwardLimit` defaults to 1). T6: check that the admin web's rewrite forwards
  `X-Forwarded-For`. Infrastructure task: set `ForwardedHeaders:KnownNetworks` (Cloudflare and Azure ranges) and
  `ForwardLimit`, or read `CF-Connecting-IP` from trusted edges only, and test that two clients behind the full chain
  get separate limits; otherwise the 20-a-minute login limit becomes platform-wide. (T6, infrastructure)
- Reset token in the link's query string (#44 M3): T6's `/reset-password` page sends `Referrer-Policy: no-referrer`,
  removes the token from the URL after reading it (`history.replaceState`) and loads nothing from other origins; or
  move the token to the fragment (`#token=`). Decide in T6. (T6)
- Password reset email: a failed send is retried by the outbox with a new token (by design), but no test makes the
  sender fail and checks the retry (S03-T2b). The reset page `/reset-password` is the admin web's (T6). (Later)
- The CLI's Serilog → `LogLevel` mapping (`UseHostLogLevels`, S03-T1a) has no test; move it into the Host and unit-test it
  if the CLI's logging grows (T1a pre-review N2). The `create-platform-admin` unique-index race path has no test (two
  concurrent runs) (T1a pre-review M2). (Later)

**First deployment (infrastructure task after M1, with Q10)**
- **Before the first public deployment (S02-T5b):** the public app forwards the `Host` header it receives to `/site`;
  behind a production proxy it must read that proxy's forwarded host instead (and the API must list the app's address
  in `ForwardedHeaders`). Set `MSP_API_URL` for the public app (default `http://localhost:5080`); without it every
  public page is a 500. After `seed --category DEMO` against a running API, a demo host may stay "unknown" for up to
  60 s (host cache); `pnpm e2e` seeds before it starts the API.
- **First deployment (S03 D14):** the data protection key ring (`identity.data_protection_keys`) is stored
  unencrypted; protect it at rest (`ProtectKeysWithAzureKeyVault`) in every deployed environment.
- **First deployment (ADR-0016 infrastructure task, with Q10):** set `Platform:BaseDomain` and
  `ForwardedHeaders:KnownProxies`/`KnownNetworks` (the public web's server) in each environment. Without proxies,
  forwarding is off and every public request through the web server resolves the API's own host → 404 (S02-T3b
  pre-review). Outside Development and Testing a missing or `localhost` base domain stops the API and the CLI (#35 R1).
  Changing the base domain later needs a data step that rewrites the existing platform subdomains (or adds new
  primaries): the backfill never touches existing rows (#35 N1).
- The API needs `ConnectionStrings:Platform` (`sp_platform`) at runtime for the audit trail (S02-T2b): provision that
  secret for the API in Key Vault with the staging setup, or every audited request fails there (#33 N1).
- SMTP provider for `IEmailSender`, `Email:From` on the real domain, and encryption at rest of the data-protection
  key ring (Key Vault) — S03 D13/D14.

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
- Merge style (owner, 2026-10-18): merge commits, not squash. For stacked PRs, retarget the next PR to `main`
  before merging it.
- The prototype's old branches (`agent/*`, `codex/*`, `feature/*`, `ci/*`) are not all covered by tag
  `prototype-v0`; keep them or tag them before deleting. (The merged S00 branches and `restart/kit` were deleted
  on 2026-10-17.)
- No GitHub Pro for now (owner, 2026-10-18): `main` stays without branch protection; merge only on green CI.
- Country `PS` keeps the CLDR name (owner, 2026-10-18).

**Later** (each item names its trigger)
- Moving `BuildingBlocks/Modules` to a `BuildingBlocks.Web` project is optional, not planned (reviewer guidance on #13).
- **Branding/PRO (custom domains):** `GetSiteHandler` throws (500) if a tenant resolves through an `ACTIVE` domain but
  has no `ACTIVE` primary one. Impossible today (the platform subdomain is always primary and `ACTIVE`); once a
  platform subdomain can be `DISABLED`, keep one active primary per visible tenant or fall back to the resolved host
  (S02-T3b pre-review).
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
- Colour-literal checks have known gaps and false positives (S00 Implementation notes, T2).
- pnpm 12's minimum-release-age policy is on: new package versions are installable about a day after release.
- The AR-33 test exempts the whole Host. When the first infrastructure adapter project lands (file storage, from
  S06), replace the exemption with an explicit allow-list of adapter assemblies (S01-T1 pre-review).
- Dependency updates are manual (exact pins everywhere): add Dependabot or Renovate for NuGet, npm and GitHub
  Actions, and pin actions by commit SHA (Phase 9 hardening at the latest).
