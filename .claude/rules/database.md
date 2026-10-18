---
paths:
  - "backend/**/Infrastructure/**"
  - "backend/**/Migrations/**"
  - "docker-compose.yml"
  - "infra/**"
---

# Database and migration rules (PostgreSQL 17, EF Core 10)

## Migrations
- One DbContext and migrations folder per module (`Infrastructure/Migrations`), default schema = module schema,
  migrations history table inside that schema.
- The module's DbContext derives from `ModuleDbContext` (`SportsPlatform.BuildingBlocks.Persistence`) and is
  registered in `<M>Module.AddServices` with `services.AddModuleDbContext<<M>DbContext>(configuration, Schema)`.
  That gives the schema, snake_case, app-generated UUIDv7 keys (`ValueGeneratedNever`), audit columns
  (`created_at/by`, `updated_at/by` as shadow properties set by `AuditColumnsInterceptor`), `xmin` as the row
  version of every `AggregateRoot`, and the history table `<schema>.__ef_migrations_history`.
- New entities: `base(NewId())` in the domain constructor; a private parameterless constructor for EF. Saving a
  new entity with an empty ID throws.
- Save with `SaveChangesResultAsync()`; a lost update returns the module's `<MODULE>_CONCURRENCY_CONFLICT` (409).
  Known gap: only the root row's `xmin` is checked; a change to only a child table or owned type doesn't touch
  the root (PROGRESS tech debt) — until fixed, change the root too when a child changes.
- Create a migration (run from the repository root; `dotnet tool restore` once first):
  ```bash
  dotnet ef migrations add <Name> --project backend/src/Modules/<M>/SportsPlatform.Modules.<M> \
    --startup-project backend/src/SportsPlatform.Host --context <M>DbContext --output-dir Infrastructure/Migrations
  ```
  Keep EF's file names (`<yyyyMMddHHmmss>_<Name>`). Migrations are marked generated code in `.editorconfig`.
- Apply: `dotnet run --project backend/tools/SportsPlatform.Cli -- migrate` (all modules in `ModuleRegistry`
  order, as `sp_migrator`), or let the API migrate on startup in Development (`Database:MigrateOnStartup`).
  Production always uses the CLI. The API itself always connects as `sp_app` (connection string `App`).
- Seed: `dotnet run --project backend/tools/SportsPlatform.Cli -- seed --category <REFERENCE|CONFIGURATION|DEMO|TEST>
  [--reset]`. Read-only global tables are written only by REFERENCE/CONFIGURATION seeders through
  `SeedContext.CreateOwnerContext<T>()`; declare each new table in `TableAccessPolicies`. Rules: data-conventions.md
  (Seeding).
- **Never edit a migration that is on `main`** (the guard hook blocks it). Add a new migration.
- Review generated SQL: names, types, nullability, defaults, indexes, constraints.
- Semantic data changes need a data migration in the same PR; destructive steps need a backup/restore note.
- Tables use `FORCE ROW LEVEL SECURITY`, so even the owner role sees no tenant rows without `app.tenant_id`.
  Data migrations that touch tenant rows run per tenant or through `sp_platform` (docs/architecture/multi-tenancy.md);
  a test proves the rows actually changed.

## Tenant tables
- `tenant_id uuid not null` on every tenant-owned table.
- In the same migration: enable + force RLS and add the `tenant_isolation` policy (use the
  `EnableTenantRls(schema, table)` helper from S02).
- Unique constraints and lookup indexes start with `tenant_id`.
- The isolation harness and RLS coverage test must pass.

## Modelling
- snake_case names; plural tables; `id uuid` primary keys (UUIDv7 from the app).
- Foreign keys **within** a module are required; **no foreign keys across module schemas**.
- Status columns: text + check constraint with the canonical values.
- Concurrency: map PostgreSQL `xmin` as the row version for mutable aggregates.
- Instants `timestamptz`; date-only `date`; money `numeric(19,4)` + currency code.
- JSONB only for genuinely variable payloads (timeline payloads, rules snapshots, definition content, audit
  metadata) — not as a shortcut to avoid modelling.
- Append-only tables (timeline events, audit): `IsAppendOnly()` in the EF configuration (no `updated_*` columns)
  and `migrationBuilder.AppendOnly(schema, table)` in the migration (grants + triggers rejecting UPDATE, DELETE
  and TRUNCATE for every role).

## Roles
- `sp_app` (application, subject to RLS, no DDL), `sp_migrator` (owner, DDL), `sp_platform` (BYPASSRLS, only for
  explicit platform services). Don't grant extra privileges to make something work — ask.
- Every table needs an access policy (docs/architecture/data-conventions.md "Table access"): read-write (default),
  `migrationBuilder.ReadOnlyForRuntimeRoles(schema, table)` for reference/global tables, or `AppendOnly`. Declare
  it in `TableAccessPolicies` (integration tests); the grants test fails for a table that isn't there.
