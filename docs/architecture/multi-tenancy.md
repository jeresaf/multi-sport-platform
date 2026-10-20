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

**As built (S02-T3a).** The Host's `TenantResolutionMiddleware` runs after audit and before authorization and sets
the scope's `TenantContext` once (D1), from the matched endpoint:
- **Tenant actions (admin API):** a caller who is not signed in gets `401 AUTH_UNAUTHENTICATED` before the header is
  looked at (S03 D4). `X-Tenant-Id` must be a GUID of a tenant the actor is a member of
  (`ITenantMembershipChecker`: Identity's directory, an `ACTIVE` row of `identity.tenant_memberships` for the
  signed-in user, read on every request, S03-T4a), else `403 AUTH_TENANT_FORBIDDEN`.
- **Platform endpoints marked `.RunsUnderRouteTenant()`** (`/tenants/{tenantId}/…`): the route's tenant through
  `ITenantDirectory.GetByIdAsync`, else `404 TENANT_NOT_FOUND`. The startup check requires a platform action and the
  route parameter. Other platform endpoints get no tenant, whatever headers are sent.
- **Public endpoints marked `.RequirePublicTenant()`** (S02-T3b, D6): the tenant of the request host through
  `ITenantDirectory.GetByHostAsync`, which matches `ACTIVE` rows of `tenancy.tenant_domains` (host lower case, no port,
  no trailing dot), else `404 TENANT_NOT_FOUND`; then the middleware sets the tenant and calls
  `ITenantAccess.CheckPublicAccessAsync`: only an `ACTIVE` tenant whose settings have `publicEnabled` is shown, else
  `403 TENANT_UNAVAILABLE` (the body says nothing more). The setting is tenant-owned, so it is read under the tenant
  (S02-T4a); a refused request ends there, so the tenant it set reaches nothing else. The startup check allows the
  marker only on `.AllowPublic()` endpoints under `/api/public`. Other public endpoints (the reference lists) get no
  tenant and answer on any host.
- **Outbox consumers and seeds:** unchanged (envelope tenant; owner contexts have none).

**Tenant settings (S02-T4a).** `tenancy.tenant_settings` is the first tenant-owned business table: one row per tenant,
created with the tenant, holding time zone, locale, currency, `publicEnabled`, `starting_soon_minutes` and
`minor_registration_consent`. `tenancy.tenants` keeps only identity, names, country and status, so platform reads of
one tenant (under the route tenant, D2) include the settings and the platform list (no tenant) doesn't.
Tenant admins read and change them at `/api/admin/v1/tenant/settings` (S02-T4b) for the tenant in `X-Tenant-Id`: the
query filter and RLS show only that tenant's row, so the handlers never take a tenant ID. The settings row has its own
`version`/`ETag`; the platform tenant's `version` is the tenant row's only.

**Public resolution as built (S02-T3b).**
- **Domains:** `tenancy.tenant_domains` is global (no RLS: hosts are looked up before a tenant is known; an RLS
  coverage exception). `host` is unique; each tenant has at most one primary domain; status follows the tenant domain
  machine (docs/domain/state-machines.md). Creating a tenant creates `<slug>.<Platform:BaseDomain>` (default
  `localhost`, Q10), `ACTIVE` and primary; the post-migration step `tenancy.platform-subdomains` backfills older tenants.
  Outside Development and Testing the base domain must be configured and not `localhost` (or `*.localhost`): the API
  refuses to start and the CLI's backfill refuses to run, so no environment gets permanent `<slug>.localhost` rows.
- **Forwarded host (D7):** `X-Forwarded-Host` counts only from `ForwardedHeaders:KnownProxies` (IP addresses) or
  `ForwardedHeaders:KnownNetworks` (CIDR); the framework's loopback defaults are removed. With neither configured,
  forwarding is off (the framework would otherwise trust every caller). Development lists loopback, where the Next.js
  server runs. The host and, since S03-T2b (owner), the client address (`X-Forwarded-For`, for the sign-in rate limits
  and the logs) are forwarded from the same trusted proxies; the scheme is not.
- **Development override:** in Development only, `X-Tenant-Slug: <slug>` or `?tenant=<slug>` names the tenant instead
  of the host; the visibility check still applies.
- **Cache:** host → tenant (summary with status) and tenant → `publicEnabled` in an in-process `TenantHostCache` for
  60 s, unknown hosts included. Creating a tenant, changing its status and changing `publicEnabled` clear the whole
  cache after they commit; other API instances catch up when their entries expire.
- **Shared caches:** tenant-public responses carry `Vary: X-Forwarded-Host` (and `X-Tenant-Slug` in Development).
- Public refusals are not audited: public endpoints declare no action.

**Public web as built (S02-T5b).** The public app (`web/apps/public`) reads the request's `Host`, drops the port and
calls `GET /api/public/v1/site` with it as `X-Forwarded-Host` (server-side, `MSP_API_URL`, no caching in the app). The
`(site)` layout renders the tenant's name and footer for a `200`; `TENANT_NOT_FOUND` (or no host) is the app's 404
not-found page ("We can't find this page", also used for unknown paths); `TENANT_UNAVAILABLE` is a `200` page with
`noindex` (owner); any other failure is a `500` "Something went wrong" page showing the Next.js error digest, logged
with the API's correlation ID. The app forwards the `Host` it receives; behind a production proxy it must read that
proxy's forwarded host instead (follow-up). Locally, `kmsa.localhost:3000` and `elssc.localhost:3000` work after
`seed --category DEMO` (browsers resolve `*.localhost` to loopback).

The actor works the same way: `ActorContext` (scoped) is the system actor until the request's authenticated principal
sets a user.

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
  services (e.g. `PlatformTenantStatistics`) and are reviewed. An architecture test (`TenantIsolationRulesTests`)
  fails on any `IgnoreQueryFilters(` under `backend/src` outside its allowlist of such services (empty today).
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
  (`sp_platform`) with `BYPASSRLS`, only inside platform services, and are audited. A platform service gets its own
  DbContext registered with `AddPlatformServiceDbContext<T>` (connection `Platform`): it maps tables that the
  module's migrated context owns, is never migrated, and an architecture test names the features allowed to use it.
  The first one is `PlatformAuditDbContext` (S02 D3). Each derives from `PlatformServiceDbContext`, maps no
  tenant-owned entity (plain row types instead, so no tenant filter applies) and is listed in
  `TenantIsolationRulesTests` with the namespaces allowed to use it (S03-T4a, #33 N2).
- **Identity directory (S03-T4a, D8):** `IdentityDirectoryDbContext`, read-only, used only by Identity's
  `Features.Directory`: the membership check above (a request's tenant isn't set yet) and the user's tenants and roles
  for `GET /api/account/v1/me` (each tenant's slug, display name and status then come from Tenancy's
  `ITenantDirectory.GetManyAsync`, the global tenant table; suspended and archived tenants are listed, S03-T6a).
  Every read is of the caller's own memberships (the current actor; no method takes a user ID), so these reads are
  **not audited**: an exemption from the rule above, agreed by the owner (S03-T4a), because the membership check runs
  on every admin request. One more read (S03-T4c2): the invitation whose token
  has a given SHA-256, returning its ID and tenant only, so the invitee's lookup and acceptance can then run under
  that tenant. That read is covered by those endpoints' own audit entries (`invitation.read`, `invitation.accept`:
  under the invitation's tenant, or without a tenant when the token is unknown).
- **Audit under RLS (as built, S02-T2b):** `audit.audit_entries` has the `tenant_isolation` policy, so `sp_app` reads
  and inserts its tenant's entries only, and entries without a tenant not at all (they would be a future tenant-facing
  audit view's rows). The writers (`AuditLog` for direct entries, `AuditRecordedConsumer` for entries through a
  module's outbox, with its `platform.inbox_messages` row in the same transaction) and the platform query
  (`GET /api/platform/v1/audit-entries`) use `PlatformAuditDbContext`. A platform action is audited with its target
  tenant: the tenant context on tenant routes (D2), or `AuditScope.Describe(targetTenantId:)` for a platform read
  filtered to one tenant (a different target than the scope's tenant is a bug and throws).
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
- **RLS coverage test** (`RlsCoverageTests`): queries `pg_class`/`pg_policies` and fails if any table with a
  `tenant_id` column, in any schema, lacks RLS enabled + forced + the `tenant_isolation` policy (all commands, every
  role, `USING` = `WITH CHECK` on `app.tenant_id`), or has another permissive policy that would widen it. Exceptions
  are listed in the test with a reason: `*.outbox_messages` (the envelope's nullable tenant; owner decision Q15).
  `inbox_messages` has no `tenant_id`. A second test fails when an exception no longer matches a table.
- **Audit under RLS** (`AuditRlsTests`, with the audit trail tests): `sp_app` sees and writes only its tenant's
  entries; the platform query sees every tenant and none; direct and outbox writes land.
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
| `BLOCKED` | Admin access denied except tenant owner viewing status (`tenant.access.read`, S03 D11); public site off |

The access-mode check runs before entitlement and permission checks for every admin request. While a tenant is `PROVISIONING` it is
`FULL` for its owners and admins only; other members get `TENANT_PROVISIONING` (docs/domain/state-machines.md).

## Platform staff and support

- Platform roles never get implicit tenant data access through the admin API. Support views are separate
  platform endpoints, read-only by default, audited per access.
- No impersonation in v1.
