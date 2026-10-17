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
- Create (S01 replaces this with the exact tested command):
  `dotnet ef migrations add <Name> --project backend/src/Modules/<M>/SportsPlatform.Modules.<M> --startup-project backend/src/SportsPlatform.Host --context <M>DbContext --output-dir Infrastructure/Migrations`
- Apply locally with the CLI `migrate` command (or dev auto-migrate). Production uses the CLI as `sp_migrator`.
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
- Append-only tables (timeline events, audit) get triggers that reject UPDATE/DELETE where specified.

## Roles
- `sp_app` (application, subject to RLS, no DDL), `sp_migrator` (owner, DDL), `sp_platform` (BYPASSRLS, only for
  explicit platform services). Don't grant extra privileges to make something work — ask.
