# Multi-tenancy

Decision: ADR-0004. This document is the implementation guide.

## Vocabulary

- **Tenant** — a customer organisation leasing the platform (federation, league, school, organiser, club).
  Not a club or team record (those live in Registry inside a tenant).
- **Platform-global data** — no `tenant_id`: reference data, sport definitions, formats, ruleset templates,
  plans, entitlement definitions, action catalogue.
- **Tenant-owned data** — everything else, with `tenant_id NOT NULL`.

## Resolving the tenant

| Caller | How tenant is resolved | Validation |
|---|---|---|
| Admin web / admin API | `X-Tenant-Id` header chosen in the tenant switcher | User must have an active membership in that tenant; otherwise `403 AUTH_TENANT_FORBIDDEN` |
| Platform console / platform API | No ambient tenant; tenant ID is an explicit route parameter (`/api/platform/v1/tenants/{id}/…`) | Platform role required; every call audited with the target tenant |
| Public web / public API | Host name → `tenancy.tenant_domains` (platform subdomain or verified custom domain) | Tenant must be publicly visible and not `BLOCKED` |
| Background jobs, outbox consumers | `tenant_id` in the job payload / event envelope | Consumer sets tenant context before touching data |
| CLI tools | Explicit `--tenant` argument | Platform credentials |

Tenant resolution results are cached briefly (domain → tenant ID); cache entries are invalidated on domain or
status changes.

## Enforcement layers

### Layer 1 — application (EF Core)

- Tenant-owned entities implement `ITenantOwned { Guid TenantId }` (get-only; no public setter).
- `ModuleDbContext` applies a named query filter (`TenantIsolation`) `e => e.TenantId == CurrentTenantId` to every
  `ITenantOwned` entity by convention. `CurrentTenantId` is the scoped `ITenantContext`'s value, read per query; no
  tenant matches no row (fail closed). The name leaves room for a module's own filters.
- `TenantOwnershipInterceptor` (a `SaveChanges` interceptor, after `MessagingInterceptor` so rows that domain event
  subscribers add are covered, before `AuditColumnsInterceptor`):
  - stamps `TenantId` on added entities whose `TenantId` is empty, through the change tracker;
  - throws if there is no tenant, or an added, modified or deleted entity's `TenantId` (current or original)
    differs from the context. A mismatch is a bug, so it is a `TenantIsolationViolationException` (`NoTenant` or
    `OtherTenant`), not an error code.
- `TenantContext.Set` is called once per scope (S02 D1): the same value again is a no-op, any other value throws.
- `IgnoreQueryFilters()` is forbidden in feature code. The only exceptions live in explicitly named platform
  services (e.g. `PlatformTenantStatistics`) and are reviewed.
- Owner contexts (seeds on `Migrator`) have no tenant, so they can't write tenant-owned rows.

### Layer 2 — database (PostgreSQL Row-Level Security)

- The application connects as role `sp_app`, which **does not own** the tables and is not a superuser, so RLS
  applies. Migrations run as `sp_migrator` (owner).
- Every tenant table gets:

```sql
ALTER TABLE registry.persons ENABLE ROW LEVEL SECURITY;
ALTER TABLE registry.persons FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON registry.persons
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
```

- Migrations call `migrationBuilder.EnableTenantRls(schema, table)`, which writes the three statements above
  (`DisableTenantRls` for a `Down` that keeps the table).
- `TenantConnectionInterceptor` (a `DbConnectionInterceptor`) runs
  `SELECT set_config('app.tenant_id', @tenantId, false)` **every time a connection is opened** — session-level,
  so it also covers EF Core queries that run outside explicit transactions. It always sets a value: the current
  tenant ID, or an empty string when there is no tenant context (which matches no rows thanks to `NULLIF`).
- Pooled connections: Npgsql resets session state before a pooled connection is reused, and the interceptor
  overwrites the value on every open anyway. A test proves a pooled connection never carries a previous
  request's tenant.
- Platform-wide operations that legitimately span tenants use a dedicated role/connection string
  (`sp_platform`) with `BYPASSRLS`, only inside platform services, and are audited.
- Global tables (no `tenant_id`) have no policy; the app role has read-only grants on them where appropriate.
- The three roles are created when the database server is provisioned, not by EF migrations. On managed
  PostgreSQL (Azure Database for PostgreSQL Flexible Server, ADR-0016) the server admin is not a superuser but can
  create `BYPASSRLS` roles on PostgreSQL 16 and later. Local and CI scripts create the same roles.
- `FORCE ROW LEVEL SECURITY` makes the table owner (`sp_migrator`) subject to the policies too. A migration
  that changes tenant rows must therefore run per tenant (setting `app.tenant_id`) or through `sp_platform`;
  otherwise it silently matches no rows.

### Layer 3 — tests

- **Isolation harness** (`SportsPlatform.IntegrationTests/Isolation`): discovers every `ITenantOwned` entity
  type from all module DbContexts (each needs a registered sample, or the harness fails), creates a row in tenant A,
  then as tenant B asserts, in EF and in raw SQL as `sp_app`: not visible, cannot update, cannot delete, cannot be
  copied into tenant A (`WITH CHECK`), cannot be referenced (composite foreign keys). How to add a sample:
  `.claude/rules/testing.md`.
- **Pool leak test:** a reused pooled connection gets the new scope's tenant, or `''` without one.
- **Consumers under RLS:** a consumer writes its envelope tenant's rows; a write for another tenant fails RLS.
- **RLS coverage test:** queries `pg_class`/`pg_policies` and fails if any table with a `tenant_id` column lacks
  RLS enabled + forced + a `tenant_isolation` policy.
- **API-level tests** for each new endpoint that takes an ID from another aggregate (e.g. adding a person to a
  team): an ID from another tenant must yield `404` (not `403` — don't reveal existence).

## Uniqueness and references

- Tenant-scoped unique keys always include `tenant_id`: `(tenant_id, slug)`, `(tenant_id, authority, value)`
  for external identifiers.
- Within a module, a foreign key between two tenant-owned tables is always composite: `(tenant_id, parent_id) →
  (tenant_id, id)`, with a unique `(tenant_id, id)` key on the parent (owner, S02-T2, 2026-10-18). PostgreSQL checks
  foreign keys without RLS, so a single-column key would let tenant B's row point at tenant A's row.
  `ModuleDbContext` refuses a single-column key between tenant-owned entities when the model is built. Keys to a
  global table (e.g. `tenancy.tenants`) stay single-column.
- Across modules there are no FKs (ADR-0001); handlers validate referenced IDs through the owning module's
  Contracts, which run under the same tenant context.

## Tenant access mode

Computed by Tenancy from tenant status × subscription status (docs/domain/state-machines.md):

| Access mode | Meaning |
|---|---|
| `FULL` | Normal operation |
| `READ_ONLY` | Admin can read and export; writes rejected with `TENANT_READ_ONLY`, except: finishing contests already `LIVE` and submitting their results (24 h window), account/billing actions, exports |
| `BLOCKED` | Admin access denied except tenant owner viewing status; public site off |

The access-mode check runs before entitlement and permission checks for every admin request.

## Platform staff and support

- Platform roles never get implicit tenant data access through the admin API. Support views are separate
  platform endpoints, read-only by default, audited per access.
- No impersonation in v1.
