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
- [x] A sample failing request returns Problem Details with `code` and `correlationId`.
- [x] CI fails if generated files are stale (demonstrated in PR description).

### T4 — Action catalogue and authorization skeleton
Deliver:
- `ActionDefinition` (fields in docs/architecture/authorization.md); each module declares its actions in one
  place; endpoint extensions `.RequireAction(...)` and `.AllowPublic()`.
- Startup/integration test: **every** endpoint declares exactly one of `.RequireAction(...)` or `.AllowPublic()`
  (fail closed). Under `/api/admin` and `/api/platform`, `.AllowPublic()` is not allowed. `GET /health/live`,
  `GET /health/ready`, `/api/public/v1/**` and the Development-only OpenAPI/Scalar endpoints are marked
  `.AllowPublic()`.
- Exporter writes `docs/generated/actions.md` and `contracts/actions.json`; CI staleness check.
- Authorization middleware (owner, 2026-10-17: middleware rather than an endpoint filter) calling
  `IAuthorizationEvaluator`; default registration **denies**; tests use a
  configurable test evaluator. Denials return `403 AUTH_FORBIDDEN` and are written to audit in T6.
- Platform actions for this slice: `reference.read` (if any admin reads), `platform.outbox.read`,
  `platform.outbox.redrive`.

Acceptance:
- [x] A test endpoint without an action fails the startup test; so does an endpoint with both markers.
- [x] Generated actions catalogue lists the Platform actions with module, scope, audit and entitlement fields.

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
- [x] Test: state change + message commit atomically (rollback leaves neither).
- [x] Test: duplicate delivery handled once; poison message ends `FAILED`; re-drive succeeds after fix.

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
- [x] Trigger test: UPDATE/DELETE on audit entries fails, both for `sp_app` (privilege) and `sp_migrator` (trigger).
- [x] Test: an audited action produces exactly one entry with correlation ID and reason.
- [x] Redaction test passes.

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
  `ErrorKind` gained `MethodNotAllowed`, `UnsupportedMediaType` and `Unexpected`. `PLATFORM_ROUTE_NOT_FOUND` only
  when no endpoint matched; a bare 404 from a matched endpoint gets no code (endpoints must use `ToProblem()`). The
  framework's `traceId` member is removed (one request ID per body). Other framework statuses still have no code:
  401/403 (T4/S03) and Kestrel's 408/413/431 (413 coded in T3b; 408 and 431 have no body). Pipeline tests run in
  Testing and Development. Errors are discovered by reflection: public static readonly `Error`
  fields of static `*Errors` classes in BuildingBlocks and each module assembly (a test fails for any other static
  `Error`). The correlation ID is `HttpContext.TraceIdentifier`. `ProblemDetails:TypeBaseUri` placeholder
  `https://errors.sportsplatform.invalid/` until Q10 (domain) is decided. Stale-file checking arrives with T3c's
  `Contracts` job.
- **T3b:** `Host/Observability` (`AddObservability`, `UseRequestLogging`, `RequestContextEnricher`) and
  `Host/Health`. Serilog JSON (`RenderedCompactJsonFormatter`) in every environment; levels from
  `Serilog:MinimumLevel`. The logger lives in DI only (`preserveStaticLogger: true`, request logging gets it via
  `options.Logger`): with the static `Log.Logger`, several hosts in one process (tests) wrote to whichever was
  built last. The enricher reads `ITenantContext`/`ICurrentActor` from `RequestServices` at log time (so S02's
  tenant appears once resolved), skips them when `RequestServices` is null, and has a per-thread re-entry guard for
  a service that logs while being built. It also drops ASP.NET Core's `RequestId` (#17 R2); enrichers and sinks
  sit in a Serilog sub-logger because Microsoft.Extensions.Logging scope properties are added after the root
  logger's enrichers. Request completion: Error for 5xx/exceptions, Verbose for the two probe
  paths, otherwise Information. OpenTelemetry: traces (ASP.NET Core without probes, HttpClient, Npgsql) and metrics
  (ASP.NET Core, HttpClient, Npgsql, `System.Runtime`); OTLP exporters only when `OTEL_EXPORTER_OTLP_ENDPOINT` is
  set (launchSettings points at the Aspire dashboard); no OTLP logs (owner). `/health/ready` runs `SELECT 1` on the
  `App` connection, 5 s timeout, 200 `{"status":"ok"}` / 503 `{"status":"unavailable"}`; failures and cancellations
  are logged as separate Warnings. Added `VALIDATION_REQUEST_TOO_LARGE` (413, `ErrorKind.PayloadTooLarge`), proven on
  real Kestrel (`KestrelLimitsTests`); Kestrel's 408/431 are rejected before the pipeline and have no body (owner).
  Tests capture telemetry with `TelemetryCapture` (DI Serilog sink + in-memory span exporter). TestServer can stop
  a request's span after the client has the response, so "not traced" is tested through the configured filter, not
  by the absence of a span.
- **T3c:** `Host/OpenApi/ApiDocumentation` (`AddApiDocumentation`, `MapApiDocumentation`, `ExportAsync`): one
  document `v1`, OpenAPI 3.1, title "MSP API", generated in every environment and served (`/openapi/v1.json`,
  Scalar at `/scalar`) in Development only. Transformers: `servers` removed (the committed file must not depend on
  the request), a `ProblemDetails` component always present with required `code` and `correlationId`, and the error
  codes of `.ProducesErrors(...)` (`SportsPlatform.BuildingBlocks.Api`, one Problem Details response per status)
  listed in each response's description and `x-error-codes`. Health probes are excluded from the document. The
  API's composition moved to `SportsPlatformApp.Build` (shared by `Program` and the CLI); it calls `UseRouting`
  and `UseEndpoints` explicitly, after every `Map…`, because `WebApplication` otherwise registers endpoints with
  routing only when the app starts and `export-openapi` builds the app without starting it (no server, no hosted
  services, no database). JSON number handling is `Strict` (the web default reads `"5"` as a number and documents
  every number as `integer | string`). `web/packages/api-client`: `pnpm generate` (openapi-typescript 7.13 →
  `src/generated/schema.ts`, committed, not linted), `createApiClient` (openapi-fetch 0.17), `ProblemDetails` type
  and `isProblemDetails` guard; openapi-typescript's `typescript ^5` peer is allowed for 6
  (`pnpm-workspace.yaml`). CI job `Contracts` runs `export-catalogues`, `export-openapi` and the client
  generator, then fails on any diff, untracked files included.
- **T4:** `ActionDefinition`, `IAuthorizationEvaluator`/`AuthorizationRequest` (action + actor; the resource scope
  chain comes with S03) and `AUTH_FORBIDDEN` live in `SportsPlatform.BuildingBlocks.Actions`; `.RequireAction(...)`
  and `.AllowPublic()` (metadata only, any endpoint builder) in `SportsPlatform.BuildingBlocks.Api`. A module's actions
  are the public static readonly `ActionDefinition` fields of its static `*Actions` class; the catalogue adds the
  module name, like the error catalogue (a test fails for any other static `ActionDefinition`). Platform actions are
  not scopable; tenant actions are always scopable by `TENANT`. `Entitlement` is a string until S04. Enforcement is a
  middleware after `UseRouting` (owner, instead of an endpoint filter): it works for every endpoint type and runs
  before body binding, so a caller who is not allowed gets 403, not 400. It passes requests with no matched endpoint
  and unmarked endpoints that are not a `RouteEndpoint` (the router's 405), evaluates a marked endpoint of any type,
  and refuses at runtime any endpoint without exactly one action. Admin/platform paths are detected from the parsed
  route segments (`~/api/admin/...` counts). Modules sharing an assembly are rejected by the action catalogue. `SportsPlatformApp.Build` throws if an endpoint breaks the rules (`EndpointActionRules`: neither or both
  markers, two actions, `.AllowPublic()` under `/api/admin` or `/api/platform`, an action not in the catalogue), so the
  API and `export-openapi` won't build with one. The public module group is `.AllowPublic()` as a group. Default
  evaluator: `DenyAllAuthorizationEvaluator`; `TestApp.Authorization` is a `TestAuthorizationEvaluator` (denies until
  `Allow(...)`/`AllowAll()`). `export-catalogues` writes `actions.md`/`actions.json`, checked by the `Contracts` job.
  Owner: `platform.outbox.read` is audit `REQUIRED`, `platform.outbox.redrive` `REQUIRED_WITH_REASON`; no
  `reference.read` (T7's reference endpoints are public). Allowed/denied tests for the two outbox actions come with
  their endpoints in T5.
- **T5 (split in two, owner 2026-10-17):** T5a = domain event dispatch, outbox and inbox in every module schema,
  `OutboxProcessor` and its hosted service; T5b = event catalogue + `events.md`/`events.json` + CI check, outbox
  diagnostics and re-drive (with the allowed/denied tests and "re-drive succeeds after fix"). Outbox statuses
  `PENDING`/`PROCESSED`/`FAILED` added to docs/domain/state-machines.md (owner). Outbox and inbox are read-write,
  with a nullable `tenant_id` and no RLS (owner; Q15 answered 2026-10-18: stays so in S02). Names: `IDomainEventSubscriber<T>` and
  `IIntegrationEventConsumer<T>` (the analyzers reserve the `EventHandler` suffix, CA1711); `ModuleDbContext.Publish`;
  `AddDomainEventSubscriber`/`AddIntegrationEventConsumer`. `MessagingInterceptor` (dispatch, then envelope) runs
  before `AuditColumnsInterceptor`. `TenantContext` and `CorrelationContext` are scoped and settable (registered by
  `AddModuleDbContext`); the correlation middleware fills `CorrelationContext`, the processor fills both from the
  envelope. `Outbox:Enabled` is false in `appsettings.Testing.json`; tests call `ProcessOnceAsync` and use
  `Outbox:MaxAttempts = 3`. A message with no consumers is `PROCESSED` (an unknown event version can't be told apart
  from an unsubscribed one). Test module `messaging_test` (`MessagingTestDbContext`, table `probes`) publishes and
  consumes.
  Pre-review (owner decision): per-aggregate order is kept strictly. `outbox_messages.sequence` is an identity
  column (migration `AddOutboxSequence`); the claim takes messages by `sequence` and skips one while an earlier
  message of its aggregate is not `PROCESSED`, so a retrying or `FAILED` message holds back its aggregate's later
  ones. Outbox IDs come from `SequentialIds` (monotonic UUIDv7) because EF Core orders one save's inserts by key (a
  test fails with random UUIDv7s). The hosted service drains (repeats while a call attempted something). Also from
  pre-review: a failing module is logged and skipped; the processor requires `ITenantContext` to be the scoped
  `TenantContext` (S02 must keep a settable context for consumers); IDs of entities added by domain event
  subscribers are checked; tests for a locked message (`SKIP LOCKED`) and for losing the inbox race.
  Review (#20 R1): a failing module backs off 1 s … 30 s (`OutboxModuleBackoff`) instead of logging an error on
  every poll; a message without consumers is logged at Debug.
- **T5b (split in two, owner 2026-10-18):** T5b-1 = event catalogue; T5b-2 = outbox diagnostics, re-drive and their
  allowed/denied tests. `Host/Catalogues/EventCatalogue` reads every `IIntegrationEvent` in each module's
  `<module assembly>.Contracts` and the `IntegrationEventSubscription`s that `AddSportsPlatform` registers (the CLI
  builds the services, no database). Payload members come from the outbox's JSON settings (`MessageSerialization`,
  read through a copy with the reflection resolver), with a simple type (`uuid`, `instant`, `integer`, `uuid[]`,
  `string (enum X: A | B)`, …) and nullability; any other member type (dictionary,
  `byte[]`, nested record, list of lists, `DateTime`) fails the export until the catalogue can describe it. The export also fails
  for a duplicate, badly named or struct event, a consumer subscribed twice, and a consumer of an undeclared event; `EventCatalogueTests` also fails for an event declared outside Contracts. No events are declared
  yet, so `events.md` says so and `events.json` is `{"events": []}`. The `Contracts` CI job already fails on any stale
  generated file. There is no `since` column: events carry no such declaration.
  Review (#21 R1, R2): `MessageSerialization` writes enums as UPPER_SNAKE strings (`JsonStringEnumConverter`,
  `SnakeCaseUpper`, numbers rejected on read), as api-conventions.md requires; `DateTime` members fail the export
  (use `DateTimeOffset` or `DateOnly`).
  T5b-2 (plan approved 2026-10-18): `OutboxDiagnostics` (BuildingBlocks.Persistence, next to the processor) lists and
  re-drives through each module's own DbContext; the Platform module maps `GET /api/platform/v1/diagnostics/outbox`
  (`module`, `status`, `limit`, `cursor`; newest first by message ID, merged across modules; no payload; `heldBackBy`)
  and `POST …/diagnostics/outbox/{module}/{messageId}/redrive` (`{"reason"}`, 1–500 characters; row locked with
  `FOR UPDATE`; `FAILED` → `PENDING`, attempts 0, due now, `last_error` kept). Errors:
  `PLATFORM_OUTBOX_MESSAGE_NOT_FOUND` (404, unknown module too), `PLATFORM_OUTBOX_MESSAGE_NOT_FAILED` (409), and in
  `CommonErrors` `VALIDATION_INVALID_QUERY` and `VALIDATION_REASON_REQUIRED` (400, with a `detail`). The cursor is the
  last ID, base64url (`OutboxCursor`). HTTP JSON now writes enums as UPPER_SNAKE strings (first endpoint with an enum).
  Request shape: FluentValidation 12.2.0 (Apache-2.0; owner on #22) validators run by `.ValidateRequest<TRequest>(error)`
  (BuildingBlocks.Api endpoint filter); failures add `errors: [{field, code}]`. Every list parameter is bound as text
  (`[AsParameters]`), so `?limit=abc` is `VALIDATION_INVALID_QUERY` like `limit=0` (#22 R1).
  The handler takes the reason but nothing records it until T6.
- **T6 (split in two, owner 2026-10-18):** T6a = `audit.audit_entries`, recording through the outbox and directly,
  redaction, the outbox diagnostics' audits (all three acceptance criteria); T6b = the query endpoint for platform
  staff. There was no command pipeline, so T6a adds the minimum: `AuditScope` (BuildingBlocks `Auditing`, scoped), begun
  by the Host's `AuditMiddleware` (after `UseRouting`, before `UseActionAuthorization`) with the endpoint's action;
  `MessagingInterceptor` puts the scope's entry in the first saving module's outbox (`AuditRecorded.V1`, declared in
  Platform's Contracts and built through `IAuditEventFactory`, because BuildingBlocks can't reference Platform); a
  failed save detaches it again. `ModuleDbContext.RecordAudit` serves work in its own scope (the re-drive commits its
  entry in the target module's outbox, #22 R2). The middleware writes the rest directly through `IAuditLog`: `DENIED`
  for 401/403, `FAILURE` for other errors and exceptions, `SUCCESS` for audited actions that saved nothing (the outbox
  list, with its filters as metadata). Owner defaults (plan, 2026-10-18): no RLS until S02; direct `SUCCESS` for actions
  that save nothing; one unit of work per command (an explicit transaction rolled back after the save loses the entry);
  validation failures audited with their code. Columns beyond pack spec 40: `error_code` (from the Problem Details
  customizer), `actor_kind` (`USER`/`SYSTEM`, spec's `actor_type`). `source` is `API` or `SYSTEM`. Redaction:
  `[PersonalData]` (BuildingBlocks `Privacy`) on metadata record properties; `AuditMetadata` writes `PRIVATE`/`SENSITIVE`
  values as `"[REDACTED]"` before the record leaves the request; `reason` is free text and not redacted. The test reset
  now covers every schema a module's model maps to (Platform's `audit`). `AuditRecorded` is listed with producer
  Platform in events.md, though every module publishes it.
  T6b (plan approved 2026-10-18): `GET /api/platform/v1/audit-entries` in Platform `Features/Audit/ListAuditEntries`,
  action `platform.audit.read` (`REQUIRED`). Filters as specified plus `outcome` (pack spec 40's Audit Explorer); no
  module filter (no column; with the Audit Explorer UI) and no "entries without a tenant" filter (with S02's RLS).
  `entityId` needs `entityType` (the `(entity_type, entity_id)` index); `action` is checked by format only
  (`ActionCode.IsValid`). Keyset paging by `(occurred_at DESC, id DESC)`, cursor `AuditCursor` (base64url of UTC ticks
  and ID). Bad parameters are `VALIDATION_INVALID_QUERY`; no new error codes, events or migrations.
- **T7 (split in two, owner 2026-10-18):** T7a = the seed framework; T7b = reference tables, data files, REFERENCE
  seeders and the public endpoints. The framework lives in `SportsPlatform.BuildingBlocks.Persistence.Seeding` (`ISeeder`,
  `SeedContext`, `SeedRunner`, `SeedEnvironmentPolicy`, `SeedRequest`, `OwnerContextFactory`) next to `MigrationRunner`,
  like the outbox processor; Platform owns the bookkeeping (`platform.seed_runs`, append-only, `ISeedRunLog` →
  `SeedRunLog`, written as the owner) and registers the runner with the host's environment name. `DeterministicIds`
  (BuildingBlocks `Domain`) makes UUIDv5s under a fixed MSP namespace (pinned by a unit test). Owner defaults (plan):
  `DEMO`/`TEST` allowed in Development, Staging and Testing; a category runs its prerequisites first (`REFERENCE`,
  `CONFIGURATION`); `--reset` (Development) is `DatabaseReset`: `DROP SCHEMA … CASCADE` for every module schema and
  model schema as the owner, then `migrate`. A skipped seeder writes nothing; a failed one stops the run and records
  nothing. Seed run IDs are `SequentialIds`, so the latest run of a key is the highest ID. Refusals are a
  `SeedReport`, not an `Error`: seeding isn't HTTP, so no error codes. Integration tests use test-only seeders (one or
  two per category; the REFERENCE one writes the read-only `conventions_test.reference_probes` as the owner).
  Review (#25): the reset also requires a local `Migrator` host (or `Seeding:AllowResetOf`) (R1), and a run holds a
  PostgreSQL advisory lock so concurrent runs are refused (R2).
