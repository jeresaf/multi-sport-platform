# S01 — Backend platform foundation

**Goal:** the technical spine every module uses — module pattern, persistence conventions, error contract,
action catalogue, outbox, audit, seed framework and test harness — proven with the Platform module.
**Modules touched:** Platform (+ BuildingBlocks, Host, Cli, tests).
**Depends on:** S00.
**Demo:** API runs against local PostgreSQL; `/scalar` shows the API; reference data endpoints return
countries/currencies/time zones; `docs/generated/actions.md`, `errors.md`, `events.md` and
`contracts/openapi.json` are generated; the TypeScript client compiles; tests show audit is append-only and the
outbox delivers exactly-once-in-effect.

Read first: docs/architecture/overview.md, rules.md, events-and-jobs.md, api-conventions.md,
data-conventions.md, authorization.md (catalogue section).

## Out of scope

Tenants, users, login, entitlements (S02–S04). Authorization **fails closed** in this slice: admin/platform
endpoints are reachable only in tests through a test evaluator.

## Tasks

### T1 — Module system and architecture tests
Deliver:
- BuildingBlocks (keep tiny, AR-26): `IModule` (name, schema, `AddServices`, `MapEndpoints`), `Result`,
  `Result<T>`, `Error` (code, message, kind), `Entity`/`AggregateRoot` with domain events, `ITenantOwned`,
  `ICurrentActor`, `ITenantContext` abstractions, endpoint metadata types for actions.
- Host: explicit module registration list; endpoint groups per family (`/api/public/v1`, `/api/admin/v1`,
  `/api/platform/v1`).
- `Modules/Platform/SportsPlatform.Modules.Platform` + `.Contracts`.
- Architecture tests (NetArchTest or equivalent): modules reference only BuildingBlocks and other modules'
  Contracts; Contracts never reference implementations; `*.Domain` namespaces don't reference EF Core or
  ASP.NET Core; BuildingBlocks references no module.

Acceptance:
- [ ] Architecture tests pass; the PR shows (pasted output) that a temporary forbidden reference makes them fail.

### T2 — Persistence conventions and test harness
Deliver:
- `ModuleDbContext` base: default schema per module, snake_case naming (EFCore.NamingConventions), UUIDv7 keys
  generated in the app, audit columns interceptor (`created_at/by`, `updated_at/by` via `ICurrentActor` +
  `TimeProvider`), `xmin` concurrency convention for aggregates, migrations history table per schema.
- Migration workflow documented in `.claude/rules/database.md` (exact `dotnet ef` commands).
- CLI `migrate` applies all module migrations in dependency order using the migrator connection; Host
  auto-migrates only when `Database:MigrateOnStartup=true` (default true in Development only).
- Test harness: Testcontainers PostgreSQL fixture (same roles as compose), Respawn reset between tests,
  `WebApplicationFactory` with test configuration, fake `TimeProvider`.
- A test-only entity proves conventions (snake_case, UUIDv7 ordering, audit columns, concurrency conflict).

Acceptance:
- [ ] Integration tests run against real PostgreSQL in CI.
- [ ] Concurrency conflict returns the expected error.

### T3 — API conventions, errors, OpenAPI and generated client
Deliver:
- Problem Details per docs/architecture/api-conventions.md; `Result` failures mapped to status + stable `code`;
  unhandled exceptions → 500 with correlation ID and no internals.
- Error-code declarations per module; exporter writes `docs/generated/errors.md` and `contracts/errors.json`.
- Correlation ID middleware (`X-Correlation-Id`), Serilog JSON logging with correlation and tenant/actor
  enrichment, OpenTelemetry (ASP.NET Core, HttpClient, Npgsql), `/health/live` and `/health/ready` (database).
- Built-in OpenAPI generation; Scalar UI in Development; CLI `export-openapi` writes `contracts/openapi.json`.
- `web/packages/api-client`: generation script (openapi-typescript + openapi-fetch) from `contracts/openapi.json`.
- CI step: regenerate OpenAPI, errors and client, then `git diff --exit-code`.

Acceptance:
- [ ] A sample failing request returns Problem Details with `code` and `correlationId`.
- [ ] CI fails if generated files are stale (demonstrated in PR description).

### T4 — Action catalogue and authorization skeleton
Deliver:
- `ActionDefinition` (fields in docs/architecture/authorization.md); each module declares its actions in one
  place; endpoint extensions `.RequireAction(...)` and `.AllowPublic()`.
- Startup/integration test: every endpoint under `/api/admin` and `/api/platform` has exactly one action.
- Exporter writes `docs/generated/actions.md` and `contracts/actions.json`; CI staleness check.
- Authorization endpoint filter calling `IAuthorizationEvaluator`; default registration **denies**; tests use a
  configurable test evaluator. Denials return `403 AUTH_FORBIDDEN` and are written to audit in T6.
- Platform actions for this slice: `reference.read` (if any admin reads), `platform.outbox.read`,
  `platform.outbox.redrive`.

Acceptance:
- [ ] A test endpoint without an action fails the startup test.
- [ ] Generated actions catalogue lists the Platform actions with module, scope, audit and entitlement fields.

### T5 — Domain events, outbox and inbox
Deliver per docs/architecture/events-and-jobs.md:
- Domain event dispatch inside a module before commit.
- `<schema>.outbox_messages` written in the same transaction; `OutboxProcessor` hosted service (batching,
  `FOR UPDATE SKIP LOCKED`, backoff, `FAILED` after N attempts).
- Consumer registration with per-consumer `<schema>.inbox_messages` for idempotency; tenant context set from the
  envelope.
- Event catalogue exporter → `docs/generated/events.md`, `contracts/events.json`; CI staleness check.
- Platform diagnostics: `GET /api/platform/v1/diagnostics/outbox?status=FAILED` and a re-drive action.

Acceptance:
- [ ] Test: state change + message commit atomically (rollback leaves neither).
- [ ] Test: duplicate delivery handled once; poison message ends `FAILED`; re-drive succeeds after fix.

### T6 — Audit
Deliver:
- `audit.audit_entries` (fields in pack spec 40 + `action_code`, `outcome`, `reason`, redacted `metadata`),
  append-only enforced by a database trigger that rejects UPDATE/DELETE.
- The command pipeline records audit automatically for actions marked `REQUIRED`/`REQUIRED_WITH_REASON`, by
  writing an `AuditRecorded` message to the module's outbox in the same transaction (Platform consumes it and
  appends the entry). Authorization denials and failures with no module transaction are written directly.
- Redaction: metadata fields classified `PRIVATE`/`SENSITIVE` are masked.
- Query endpoint for platform staff (filters: tenant, actor, action, entity, date range, correlation ID).

Acceptance:
- [ ] Trigger test: UPDATE/DELETE on audit entries fails.
- [ ] Test: an audited action produces exactly one entry with correlation ID and reason.
- [ ] Redaction test passes.

### T7 — Seed framework and reference data
Deliver:
- `ISeeder` (category `REFERENCE | CONFIGURATION | DEMO | TEST`, order, idempotent run), deterministic IDs for
  seeded records (UUIDv5 from a stable key), `platform.seed_runs` bookkeeping with checksums.
- Environment guards: `DEMO`/`TEST` only in Development/Staging; `--reset` only in Development.
- CLI `seed --category <c> [--reset]`.
- Reference data in Platform: countries (ISO 3166-1), currencies (ISO 4217), IANA time zones, languages
  (ISO 639-1 subset). Public endpoints `/api/public/v1/reference/{countries|currencies|time-zones|languages}`
  (cacheable).

Acceptance:
- [ ] Running the reference seed twice changes nothing (test).
- [ ] Demo seeding is refused in a Production environment (test).

## Slice acceptance

- [ ] API boots against compose PostgreSQL; migrations apply via CLI; `/health/ready` is healthy.
- [ ] Generated catalogues (actions, errors, events), OpenAPI and TS client are current and checked in CI.
- [ ] Architecture, integration and unit tests green in CI.
- [ ] `.claude/rules/database.md` and CLAUDE.md commands updated with real commands.
