# Data conventions (the "Data Integrity Manager", as rules + tests)

These conventions replace the owner's "Data Integrity Manager" and the pack's spec 43. They are enforced by EF
Core conventions, migrations review, database constraints and tests — not by a runtime module.

## Schemas and naming

- One PostgreSQL schema per module (ADR-0001). Tables plural snake_case (`team_memberships`); columns
  snake_case; primary key `id`; foreign keys `<entity>_id`; booleans `is_`/`has_`; timestamps `_at`; dates `_on`
  or `_date` (pick `_date` and keep it).
- EF Core: `EFCore.NamingConventions` snake_case; each module sets `HasDefaultSchema("<schema>")` and its own
  migrations history table in that schema.
- Code vocabulary follows docs/product/glossary.md (`contest` not `match` in generic code; `person` not `player`
  for identity; `edition` not `season` in code).

## Identifiers

- Primary keys: `uuid`, generated in the application as UUIDv7 (`Guid.CreateVersion7()`), time-ordered for index
  locality.
- Human-readable codes/slugs where useful (`slug` unique per tenant for public entities; `code` unique globally
  for platform configuration).
- External identifiers in `<schema>.external_identifiers(tenant_id, entity_type, entity_id, authority, value,
  valid_from, valid_to)` with unique `(tenant_id, authority, entity_type, value)`. Authorities are codes like
  `FUFA`, `FIFA`, `SCHOOL_ADMISSION_NO`, `PROVIDER_X`.

## Standard columns

| Column | Where | Notes |
|---|---|---|
| `tenant_id uuid not null` | tenant-owned tables | AR-04; RLS policy required |
| `created_at timestamptz`, `created_by uuid` | all mutable tables | set by interceptor |
| `updated_at timestamptz`, `updated_by uuid` | all mutable tables | set by interceptor |
| `xmin` (system column) | mutable aggregates | mapped as concurrency token (`version` in DTOs) |
| `status text` | lifecycle entities | values from docs/domain/state-machines.md; check constraint |
| `source text`, `import_job_id uuid null` | entities that can be imported | provenance (AR-22) |

## Deleting, archiving, history

- **No generic `is_deleted` soft-delete flag.** Lifecycle statuses (`ARCHIVED`, `MERGED`, `RETIRED`,
  `DISSOLVED`…) express what happened.
- Hard delete is allowed only for drafts never referenced elsewhere and for transient data (expired idempotency
  keys, temp uploads), via explicit policy.
- History patterns:
  - **Effective dating** for relationships (`start_date`, `end_date`): memberships, staff assignments, roster
    entries, sponsor campaigns.
  - **Versions** for things that change but must be reproducible: sport definitions, ruleset snapshots, schedule
    versions, result versions, brand versions.
  - **Status history** tables for lifecycle transitions with reason/actor: tenants, editions, contests,
    registrations, rosters.
  - **Append-only** for timeline events and audit.
- Merges keep the losing record with `status = MERGED` and `merged_into_id`; references are repointed by the
  owning module's merge command, and every repoint is audited.

## Time

- `timestamptz` for instants (stored UTC); `date` for birth dates and other date-only values.
- Scheduled starts are instants plus the venue's IANA time zone for display.
- Inject `TimeProvider`; tests use a fake clock. Never `DateTime.Now`/`UtcNow` in domain or handlers.
- A tenant's calendar days (subscription periods, S04) are `date`s in the tenant's time zone (settings): "today" is
  computed from the clock on every use (`TenantCalendar.Today`), never stored or cached.

## Money and units

- Money: `numeric(19,4)` + ISO 4217 currency code (fees, later).
- Height in centimetres, weight in kilograms, distances in metres, durations in seconds, times as ISO 8601
  durations in APIs where human-facing.

## Personal-data classification (ADR-0015)

Every column holding personal data is tagged in its EF configuration (e.g. `.HasAnnotation("pii", "PRIVATE")`)
so tests and exports can enforce it:

| Class | Examples | Allowed in |
|---|---|---|
| `PUBLIC` | display name, public photo, nationality (if public profile on), team, positions | public APIs/read models |
| `INTERNAL` | full legal name, gender, height/weight (unless sport policy makes them public) | admin APIs with `person.read` |
| `PRIVATE` | full date of birth, contacts, guardian details, identity documents metadata | admin APIs with `person.read_private`; never logs/analytics |
| `SENSITIVE` | national ID numbers (if ever stored) | encrypted column; explicit permission; never exported by default |

Rules: public DTO tests assert no non-`PUBLIC` fields; redaction tests assert logs/analytics/audit metadata don't
contain `PRIVATE`/`SENSITIVE` values; minors' public profiles follow ADR-0015.

Exception (owner, S03-T4b): a member's sign-in email (`identity.users`, `PRIVATE`) is shown in the member list and
detail (`/api/admin/v1/members`) to every holder of `membership.read`, without a `*.read_private` action: until
people have profiles it is the only way to tell members apart. It stays out of logs and audit metadata (the audit
names the membership by ID). The same holds for an invitation's email (`identity.invitations`, `PRIVATE`) in
`/api/admin/v1/invitations` (S03-T4c).

Properties of DTOs and audit metadata records are classified with `[PersonalData(PersonalDataClass.X)]`
(`SportsPlatform.BuildingBlocks.Privacy`; on a positional record, `[property: PersonalData(...)]`). Audit metadata
masks `PRIVATE` and `SENSITIVE` values as `"[REDACTED]"` (`AuditMetadata`). Unclassified members are written as they
are, so anything personal goes in a record with classified properties, never in an anonymous type or dictionary.

## Serializing changes: advisory locks

When a rule spans rows that a single constraint can't check (the last owner of a tenant, non-overlapping subscription
periods, a quota), the command takes a PostgreSQL transaction-level advisory lock, then reads and writes in the same
transaction. Concurrent commands on the same key queue on the lock, and each reads what the earlier ones committed.

- Key: `<schema>.<purpose>:<id>`, hashed with `pg_advisory_xact_lock(hashtextextended(<key>, 0))`, e.g.
  `identity.tenant_owners:<tenantId>` (S03), `tenancy.subscription:<tenantId>`,
  `tenancy.entitlement_override:<tenantId>` and `identity.staff_users:<tenantId>` (S04).
- Take the lock inside an explicit transaction (`BeginTransactionAsync`), before the reads the rule depends on; commit
  with `CancellationToken.None` after the save, so a client hanging up can't roll back an audited change.
- One key per rule and owner (usually the tenant); never hold two keys at once.

## Duplicate prevention

Registries (people, organisations, teams, venues, competitions) implement: normalised name fields, aliases,
external identifiers, a duplicate-check query, a candidate review workflow, and merge history. Never
auto-merge. Person rules are specified in docs/build/slices/S06-people-registry.md.

## Migrations

- Generated by EF Core per module; reviewed like code. Names: EF Core's `<yyyyMMddHHmmss>_<ShortDescription>`
  (keep the generated timestamp; don't rename files).
- **Never edit a migration that is on `main`** — add a new one (a hook blocks edits; see .claude/hooks).
- Every new tenant table: RLS enable + force + `tenant_isolation` policy in the same migration
  (`EnableTenantRls(schema, table)`). Foreign keys between tenant-owned tables are composite `(tenant_id, x_id)`
  (docs/architecture/multi-tenancy.md, "Uniqueness and references").
- Data migrations accompany semantic changes; reversible where practical; destructive steps need a reviewed
  backup/restore note in the PR.
- Production applies migrations with the CLI (`migrate`) as `sp_migrator`, never automatically at app start.
- **Post-migration steps** (`IPostMigrationStep`, registered with `services.AddPostMigrationStep<T>()`): data work a
  migration can't do because it needs configuration. `MigrationRunner` runs every step after all migrations, on each
  run, so a step must be idempotent; it writes as the owner through `OwnerContextFactory` (no tenant, so never
  tenant-owned rows). First one: `tenancy.platform-subdomains` gives each tenant without a domain
  `<slug>.<Platform:BaseDomain>` (S02-T3b).

## Table access

`01-roles.sh` gives `sp_app` and `sp_platform` SELECT, INSERT, UPDATE and DELETE on every table `sp_migrator`
creates (never TRUNCATE). Each table gets one policy, set in the migration that creates it:

| Policy | Runtime roles may | How |
|---|---|---|
| Read-write | SELECT, INSERT, UPDATE, DELETE | default, no call |
| Read-only (reference and configuration tables) | SELECT | `migrationBuilder.ReadOnlyForRuntimeRoles(schema, table)`; seeds run as `sp_migrator` |
| Append-only (audit, timeline) | SELECT, INSERT | `migrationBuilder.AppendOnly(schema, table)` + `IsAppendOnly()` in the EF configuration |
| Migrations history | nothing | revoked by the `migrate` runner after every run |

- Append-only tables also get triggers that reject UPDATE, DELETE and TRUNCATE for every role, the owner
  included (AR-19). They can be bypassed only by a superuser with `session_replication_role = replica` (the test
  reset; managed PostgreSQL in production gives the app no superuser) or by the owner changing the triggers in DDL,
  which happens only in a reviewed migration.
- Partitioned tables: grants and the TRUNCATE trigger on the parent don't reach the partitions, so apply the
  helper to each partition too (the grants test flags undeclared partitions).
- Read-only covers `sp_platform` too: it bypasses tenant isolation for cross-tenant work; it doesn't write
  reference data. A platform service that must write a global table is a declared exception (below).
- The grants test (`TableGrantsTests`) reads the privileges (table- and column-level) of every table, view,
  materialized view and foreign table and compares them with
  `TableAccessPolicies` in the integration tests. A new table fails until its policy is declared there;
  exceptions for one role are declared there with a reason.

## Seeding

- Seeders implement `ISeeder` (`SportsPlatform.BuildingBlocks.Persistence.Seeding`) and are registered with
  `services.AddSeeder<T>()` in the module. Each has a stable `Key` (`<module>.<category>.<set>`), a category, an
  `Order` within the category and a `Checksum` that changes whenever what it writes changes (data + seeder version).
- Categories: `REFERENCE` and `CONFIGURATION` write read-only global tables, so they run as the owner
  (`SeedContext.CreateOwnerContext<T>()`, on the `Migrator` connection) in every environment. `DEMO` and `TEST` go
  through application commands as `sp_app` (AR-22) and run only in Development, Staging and Testing. Running a
  category runs `REFERENCE`, then `CONFIGURATION`, then it.
- Runs are idempotent: a seeder upserts by its natural key and changes nothing when the data is already there. Seeded
  records get `DeterministicIds.Create("<module>.<entity>:<natural key>")` (UUIDv5), the same in every environment;
  never change a key once records exist. Seeders never delete: a record dropped from the data is deactivated.
- `platform.seed_runs` (append-only) records each completed run with its checksum, environment and counts. A seeder
  whose last run has the same checksum is skipped; a seeder that throws stops the run and records nothing.
- One seed run at a time per database: the runner holds a PostgreSQL advisory lock (`SeedRunner.LockKey`) for the whole
  run, and a second run is refused.
- `seed --reset` drops every module schema as the owner and applies the migrations again before seeding (append-only
  tables can't be truncated, even by the owner). It needs Development **and** a local `Migrator` host (localhost,
  loopback, a Unix socket or the compose service `postgres`); a non-local Development database must be named in
  `Seeding:AllowResetOf`.
- Reference data (Platform, S01-T7b): `platform.countries` (ISO 3166-1), `currencies` (ISO 4217), `languages`
  (ISO 639-1) and `time_zones` (IANA), read-only for the runtime roles, keyed by `code`, with `is_active`. The data
  files are embedded in the Platform module (`Seed/Reference/Data`, sources and licences in its README) and are
  regenerated by `backend/tools/reference-data/build-reference-data.cs`, never edited by hand. Names are English
  (CLDR) for now.
- Configuration data (Tenancy, S04-T1b): `tenancy.configuration.plans` writes `tenancy.plans` (code, name, rank,
  `is_active`) and `tenancy.plan_entitlements` (plan, entitlement code, value as `jsonb`), read-only for the runtime
  roles, from the plans declared in code (`TenancyPlans`, read through `IPlanCatalogue`); IDs `tenancy.plan:<code>`
  and `tenancy.plan_entitlement:<plan>:<entitlement>`. A value is `{ "enabled": bool }`, `{ "limit": n }` (`null` =
  unlimited) or `{ "level": code }` (`null` = none), the one JSON shape of `EntitlementValueJsonConverter`. A value
  changes by a reviewed change to `TenancyPlans`, then `seed --category CONFIGURATION`; a plan dropped from the code is
  deactivated, and a value for an entitlement no longer declared stays but is never read.
- Configuration data (Sports Configuration, S05-T1b1): `config.configuration.sports` writes `config.sport_definitions`
  (variant, sport, version, name, scoring family, shape, status `PUBLISHED`, content as `jsonb` written from its RFC
  8785 canonical text, content hash, published at; `jsonb` keeps the tree, not the text, so canonicalize again to
  re-hash), read-only for the runtime roles, from the definition files embedded in the module (`Definitions/Sports`);
  IDs `config.sport_definition:<variant>@<version>`. Unlike the upserting seeders, it only inserts: a published version
  is immutable (S05 D4), so a stored version whose file now hashes differently fails the run with
  `SPORT_DEFINITION_IMMUTABLE` (two files declaring one version: `SPORT_DEFINITION_DUPLICATE_VERSION`), and a file
  dropped from the module leaves its rows as they are (every row is `PUBLISHED`; there is nothing to deactivate).
- Integration tests seed the real reference and configuration data once per run, and the per-test reset keeps it
  (`PostgresFixture.ReferenceTables`, `PostgresFixture.PlanTables`, `PostgresFixture.ConfigurationTables`); a test that
  changes it calls `RestoreSeedDataAsync`.
- DEMO and TEST seeders call application commands through `SeedContext.RunCommandAsync(action, tenantId, …)`: each
  command runs in a new scope under that tenant (set once, before any connection opens; `null` leaves it for a command
  that sets its own, like tenant creation), as the system actor, with its
  audit begun as source `SYSTEM`, so its first save commits the audit entry with the change. A seeder runs a command
  only for a change; one that saves nothing or fails throws and stops the run. `SeedContext.QueryAsTenantAsync` reads
  tenant-owned data to decide what to run (not audited). Seeded tenants: `tenancy.demo.tenants` (KMSA and ELSSC).
  Seeded invitations: `identity.demo.invitations` (S03 D18, after the tenants) invites the pilot people by email
  through `membership.invite`; they get no deterministic IDs (a person may be invited several times; the natural key
  is tenant + normalised email), and the emails go out when the API's outbox runs (the CLI doesn't send).

## CI checks (automate as they become relevant)

Tenant column + RLS presence · table grants match the declared access policy · forbidden cross-module references · public DTO field classification ·
stale generated catalogues (actions, errors, events, OpenAPI) · published configuration hash unchanged ·
no `IgnoreQueryFilters()` outside allow-listed platform services · no `DateTime.Now` in domain/handlers.
