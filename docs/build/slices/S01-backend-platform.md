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
data-conventions.md, authorization.md (catalogue section), multi-tenancy.md (Layer 2 roles), and the S00
Implementation notes (test command, database roles, CI job names).

## Carried over from S00 (slice review 2026-10-17)

These are part of S01's scope; each is assigned to a task below.

- Replace the placeholder `BuildingBlocksAssemblyTests` (and the placeholder architecture test) with real tests (T1).
- Mark `GET /health/live` and `GET /health/ready` with `.AllowPublic()` (T4).
- Narrow the default DML grants from `infrastructure/local/postgres/init/01-roles.sh` (T2 helpers, applied in T2,
  T5, T6, T7).
- The Testcontainers fixture runs the same `01-roles.sh` as docker compose (T2).
- The test command is `dotnet test --solution backend` everywhere (S00 deviation).

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
  ASP.NET Core; BuildingBlocks references no module; cloud-provider SDKs (`Azure.*`) are referenced only by
  infrastructure adapters (AR-33). These replace the S00 placeholder tests.
- `ICurrentActor` has a **system actor** for work with no user (CLI, seeds, outbox processor) so audit columns
  are never empty.

Acceptance:
- [x] Architecture tests pass; the PR shows (pasted output) that a temporary forbidden reference makes them fail.

### T2 — Persistence conventions and test harness
Deliver:
- `ModuleDbContext` base: default schema per module, snake_case naming (EFCore.NamingConventions), UUIDv7 keys
  generated in the app, audit columns interceptor (`created_at/by`, `updated_at/by` via `ICurrentActor` +
  `TimeProvider`), `xmin` concurrency convention for aggregates, migrations history table per schema.
- `dotnet-ef` pinned in a local tool manifest (`.config/dotnet-tools.json`); migration workflow documented in
  `.claude/rules/database.md` with the exact commands (`dotnet tool restore` first).
- Three named connection strings: `App` (`sp_app`), `Migrator` (`sp_migrator`), `Platform` (`sp_platform`).
  Development values match `.env.example` (they are public, development-only); production values come from Key
  Vault (ADR-0016). Document them in the README.
- CLI `migrate` applies all module migrations in dependency order using the `Migrator` connection; Host
  auto-migrates only when `Database:MigrateOnStartup=true` (default true in Development only), also as
  `sp_migrator`. The API itself always runs as `sp_app`.
- **Grant narrowing.** `01-roles.sh` gives `sp_app` and `sp_platform` DML on every table `sp_migrator` creates.
  Provide migration helpers and use them from the first table on:
  - `ReadOnlyForRuntimeRoles(schema, table)` — `REVOKE INSERT, UPDATE, DELETE` from `sp_app` (reference and
    global tables; seeds for them run as `sp_migrator`);
  - `AppendOnly(schema, table)` — `REVOKE UPDATE, DELETE` from `sp_app` and `sp_platform`, plus the trigger
    required by AR-19 (used in T6, later for timelines). Decide whether the trigger also blocks `TRUNCATE` by the
    owner; the test reset (below) must still work;
  - after `migrate`, revoke all DML on each schema's migrations-history table from `sp_app` and `sp_platform`
    (EF creates that table outside the migrations, so a migration can't do it).
  A grants test reads `has_table_privilege(...)` for every table and compares it with the declared policy, so a
  new table without a decision fails.
- Test harness:
  - Testcontainers PostgreSQL with the **same image tag as docker-compose.yml** and
    `infrastructure/local/postgres/init/01-roles.sh` copied into `/docker-entrypoint-initdb.d/` (same env vars
    as compose). The fixture waits until it can connect as `sp_migrator` over TCP — not just until the server
    answers — because the init script runs before the real server starts.
  - Migrations run as `sp_migrator`; the app under test runs as `sp_app`. **No application code under test
    connects as the container's superuser** (a superuser bypasses RLS, which would make S02's isolation tests
    meaningless). A test asserts `current_user = 'sp_app'` on the app's connection.
  - Respawn reset between tests runs on a separate **test-only superuser connection** (from S02, `sp_app` sees
    one tenant only and `FORCE ROW LEVEL SECURITY` also hides rows from `sp_migrator`). It skips the migrations
    history tables and REFERENCE tables (or reseeds them), and it must work alongside T6's append-only trigger —
    choose and document how (e.g. TRUNCATE as superuser), with a test.
  - `WebApplicationFactory` with test configuration; fake `TimeProvider`.
- A test-only entity proves conventions (snake_case, UUIDv7 ordering, audit columns, concurrency conflict).

Acceptance:
- [x] Integration tests run against real PostgreSQL in CI, using the roles from `01-roles.sh`.
- [x] The app's connection is `sp_app` (test); migrations are owned by `sp_migrator` (test).
- [x] The grants test passes and fails when a table has no declared policy (show it in the PR).
- [x] Concurrency conflict returns the expected error.

### T3 — API conventions, errors, OpenAPI and generated client

May be delivered as two PRs (T3a: Problem Details, error catalogue, correlation, logging, OpenTelemetry, health;
T3b: OpenAPI, Scalar, `export-openapi`, TypeScript client, `Contracts` CI job) to keep reviews small.

Deliver:
- Problem Details per docs/architecture/api-conventions.md; `Result` failures mapped to status + stable `code`;
  unhandled exceptions → 500 with correlation ID and no internals.
- Error-code declarations per module; exporter writes `docs/generated/errors.md` and `contracts/errors.json`.
- Correlation ID middleware (`X-Correlation-Id`), Serilog JSON logging with correlation and tenant/actor
  enrichment, OpenTelemetry (ASP.NET Core, HttpClient, Npgsql), `/health/live` and `/health/ready` (database).
- Built-in OpenAPI generation; Scalar UI in Development; CLI `export-openapi` writes `contracts/openapi.json`.
- `web/packages/api-client`: generation script (openapi-typescript + openapi-fetch) from `contracts/openapi.json`.
- CI: a new job named **`Contracts`** (needs both .NET and Node) regenerates OpenAPI, errors and the client,
  then runs `git diff --exit-code`. Add the job name to the S00 Implementation notes' list of check names.
  Generated files under `contracts/` and `docs/generated/` are written by the CLI only (Claude Code's settings
  deny hand edits there).
- `/health/ready` checks the database with the `App` connection and returns only a status (no versions, host
  names or error text).

Acceptance:
- [ ] A sample failing request returns Problem Details with `code` and `correlationId`.
- [ ] CI fails if generated files are stale (demonstrated in PR description).

### T4 — Action catalogue and authorization skeleton
Deliver:
- `ActionDefinition` (fields in docs/architecture/authorization.md); each module declares its actions in one
  place; endpoint extensions `.RequireAction(...)` and `.AllowPublic()`.
- Startup/integration test: **every** endpoint declares exactly one of `.RequireAction(...)` or `.AllowPublic()`
  (fail closed). Under `/api/admin` and `/api/platform`, `.AllowPublic()` is not allowed. `GET /health/live`,
  `GET /health/ready`, `/api/public/v1/**` and the Development-only OpenAPI/Scalar endpoints are marked
  `.AllowPublic()`.
- Exporter writes `docs/generated/actions.md` and `contracts/actions.json`; CI staleness check.
- Authorization endpoint filter calling `IAuthorizationEvaluator`; default registration **denies**; tests use a
  configurable test evaluator. Denials return `403 AUTH_FORBIDDEN` and are written to audit in T6.
- Platform actions for this slice: `reference.read` (if any admin reads), `platform.outbox.read`,
  `platform.outbox.redrive`.

Acceptance:
- [ ] A test endpoint without an action fails the startup test; so does an endpoint with both markers.
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
  append-only: `AppendOnly(...)` from T2 (grants) plus a database trigger that rejects UPDATE/DELETE even for the
  owner.
- The command pipeline records audit automatically for actions marked `REQUIRED`/`REQUIRED_WITH_REASON`, by
  writing an `AuditRecorded` message to the module's outbox in the same transaction (Platform consumes it and
  appends the entry). Authorization denials and failures with no module transaction are written directly.
- Redaction: metadata fields classified `PRIVATE`/`SENSITIVE` are masked.
- Query endpoint for platform staff (filters: tenant, actor, action, entity, date range, correlation ID).

Acceptance:
- [ ] Trigger test: UPDATE/DELETE on audit entries fails, both for `sp_app` (privilege) and `sp_migrator` (trigger).
- [ ] Test: an audited action produces exactly one entry with correlation ID and reason.
- [ ] Redaction test passes.

### T7 — Seed framework and reference data
Deliver:
- `ISeeder` (category `REFERENCE | CONFIGURATION | DEMO | TEST`, order, idempotent run), deterministic IDs for
  seeded records (UUIDv5 from a stable key), `platform.seed_runs` bookkeeping with checksums. Reference tables
  are `ReadOnlyForRuntimeRoles`; the REFERENCE seeder therefore runs with the `Migrator` connection.
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
- [ ] The S00 carry-overs above are done and removed from PROGRESS.md "Tech debt / follow-ups".

## Implementation notes

- **T1:** BuildingBlocks may use ASP.NET Core only in `SportsPlatform.BuildingBlocks.Modules` (`IModule`,
  `ApiEndpointGroups`) and never EF Core; `LayeringTests` enforces it. Architecture tests find assemblies by
  scanning the test output folder (the test project references the Host, which references every module), and a
  test fails if a project under `src/Modules` is missing from the scan. `SystemActor.Id` is
  `00000000-0000-7000-8000-000000000001`. UUIDv7 IDs order by millisecond only; within one millisecond they are
  random.
- **T2 (split, agreed):** T2a = conventions + harness; T2b = grant narrowing (helpers, `AppendOnly` trigger,
  history-table revoke, grants test, `check-roles.sh`). `ModuleDbContext` lives in
  `SportsPlatform.BuildingBlocks.Persistence`; modules (not Contracts) may reference it. `Entity()` no longer
  generates an ID (EF path); domain code uses `base(NewId())`. Audit columns are shadow properties; `xmin` is the
  row version of every `AggregateRoot`. Concurrency loss → `<MODULE>_CONCURRENCY_CONFLICT` via
  `SaveChangesResultAsync`. Migration files keep EF's `yyyyMMddHHmmss_` prefix (data-conventions.md updated).
  The CLI references the Host (module list, services, appsettings); `dotnet run` uses Development via
  launchSettings, a published CLI defaults to Production. Integration tests: one container per run, database
  tests serialised in the `Database` collection.
- **T2b:** `ReadOnlyForRuntimeRoles` revokes writes from `sp_app` **and** `sp_platform` (owner, 2026-10-17, on
  the reviewer's advice in #14). `AppendOnly` revokes UPDATE/DELETE and adds two triggers per table
  (`append_only_rows`, `append_only_truncate`) calling `<schema>.reject_append_only_change()` (one function per
  schema), which raises SQLSTATE `P0001` for every role, the owner included; TRUNCATE is blocked too (owner). The
  `migrate` runner revokes all access to each `__ef_migrations_history` after every run. The declared policies
  live in `TableAccessPolicies` (integration tests); `TableGrantsTests` checks all seven table privileges for both
  runtime roles (column grants included) on every table, view, materialized view and foreign table outside the
  system schemas. The test reset uses an unpooled superuser connection
  with `session_replication_role = replica`. Test-only tables `conventions_test.reference_probes` and
  `append_only_probes` exercise the helpers until T5–T7 add real ones. The reset still truncates read-only
  (reference) tables; keeping or reseeding them is T7's.
- **T3 (split in three, owner 2026-10-17):** T3a = Problem Details, `Result` → HTTP mapping, exception handler,
  correlation ID, error catalogue + `export-catalogues`; T3b = Serilog, OpenTelemetry, `/health/ready`; T3c =
  OpenAPI, Scalar, `export-openapi`, TS client, `Contracts` CI job (which also checks the error catalogue).
  `Result` → HTTP helpers live in `SportsPlatform.BuildingBlocks.Api`, the second BuildingBlocks namespace
  allowed to use ASP.NET Core. Pipeline error codes: `PLATFORM_UNEXPECTED_ERROR` (500),
  `PLATFORM_ROUTE_NOT_FOUND` (404), `PLATFORM_METHOD_NOT_ALLOWED` (405), `VALIDATION_MALFORMED_REQUEST` (400),
  plus `VALIDATION_UNSUPPORTED_MEDIA_TYPE` (415, found while testing: minimal APIs answer 415 for non-JSON bodies).
  `ErrorKind` gained `MethodNotAllowed`, `UnsupportedMediaType` and `Unexpected`. Other framework statuses without a
  code (401, 403) get theirs from T4/S03. Errors are discovered by reflection: public static readonly `Error`
  fields of static `*Errors` classes in BuildingBlocks and each module assembly (a test fails for any other static
  `Error`). The correlation ID is `HttpContext.TraceIdentifier`. `ProblemDetails:TypeBaseUri` placeholder
  `https://errors.sportsplatform.invalid/` until Q10 (domain) is decided. Stale-file checking arrives with T3c's
  `Contracts` job.
