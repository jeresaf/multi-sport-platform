# S02 — Tenancy core and isolation

**Goal:** tenants exist, every request knows its tenant, and tenant data is isolated in the application **and**
the database — proven by tests that every later slice reuses.
**Modules touched:** Tenancy & Commercial (new), Platform, Host, public web.
**Depends on:** S01.
**Demo:** platform API creates/suspends tenants with history and audit; `demo.localhost:3000` and
`schools.localhost:3000` show different tenants; an unknown host shows a friendly page; isolation tests prove
cross-tenant access fails at both layers.

Read first: ADR-0004, docs/architecture/multi-tenancy.md, docs/domain/state-machines.md (tenant section).

## Out of scope

Users and login (S03) — tests use a test authentication scheme that supplies actor and memberships as claims.
Plans/subscriptions (S04) — access mode is status-only for now (`ACTIVE`/`PROVISIONING` → `FULL`,
`SUSPENDED`/`ARCHIVED` → `BLOCKED`).

## Tasks

### T1 — Tenants and lifecycle
Deliver:
- `tenancy.tenants` (platform-managed; no RLS): `id, slug, legal_name, display_name, country_code, time_zone,
  default_locale, currency_code, status, public_enabled, timestamps, version`; `tenancy.tenant_status_history`.
- Actions (platform): `tenant.create`, `tenant.update`, `tenant.activate`, `tenant.suspend` (reason),
  `tenant.restore`, `tenant.archive` (reason). Endpoints under `/api/platform/v1/tenants`.
- Validation: slug `[a-z0-9-]{3,40}`, reserved words (`www`, `api`, `admin`, `app`, `platform`, `static`,
  `assets`, `mail`, `status`, `docs`), never reused after archive; country/time zone/currency validated through
  Platform Contracts.
- Integration events `TenantCreated`, `TenantStatusChanged`.

Acceptance:
- [ ] Invalid transitions return `409 TENANT_INVALID_TRANSITION`; reasons required where specified.
- [ ] Status history and audit entries recorded for each transition.

### T2 — Domains and settings
Deliver:
- `tenancy.tenant_domains` (host unique, type `PLATFORM_SUBDOMAIN | CUSTOM`, `is_primary`, status); creating a
  tenant creates `<slug>.<Platform:BaseDomain>` (dev default `localhost`).
- `tenancy.tenant_settings` (**tenant-owned**, RLS) with typed, allow-listed keys: time zone, locale, currency,
  public visibility, `starting_soon_minutes`, `minor_registration_consent` (`REQUIRED | RECOMMENDED | OFF`).
- Admin endpoints `GET/PATCH /api/admin/v1/tenant/settings` (actions `tenant.settings.read`,
  `tenant.settings.update`).

Acceptance:
- [ ] Unknown setting keys rejected; values type-checked.
- [ ] Settings are the first table covered by the isolation harness (T4).

### T3 — Tenant context and resolution
Deliver:
- Admin: `X-Tenant-Id` header validated against the actor's memberships (interface `ITenantMembershipChecker`;
  S02 implementation reads test claims; S03 replaces it).
- Public: host → `tenant_domains` lookup with short-lived cache and invalidation on domain/status change. In
  Development only, `X-Tenant-Slug` header or `?tenant=` query may override (for browsers that don't resolve
  `*.localhost`).
- Jobs/consumers: tenant from the event envelope.
- `ITenantContext` populated for every request; access mode check (status-only) before authorization.
- `/api/public/v1/site` returns the tenant's public profile (display name, primary domain, public flags).

Acceptance:
- [ ] Wrong or missing tenant header → `403 AUTH_TENANT_FORBIDDEN`; unknown host → `404 TENANT_NOT_FOUND`;
      suspended tenant → `403 TENANT_BLOCKED` (admin) / unavailable page (public).

### T4 — Isolation enforcement (application + database)
Deliver:
- EF Core global filter convention for all `ITenantOwned` entities in `ModuleDbContext`; SaveChanges
  interceptor stamps/validates `tenant_id`.
- RLS: connection interceptor sets `app.tenant_id` on every connection open (session-level; always set —
  empty string when there is no tenant; policies use `NULLIF(…, '')::uuid` per multi-tenancy.md); migration
  helper `EnableTenantRls(schema, table)` (enable + force + policy); app runs as `sp_app`.
- Tests (reused by every later slice):
  - **Isolation harness** discovering all `ITenantOwned` entities across DbContexts: cross-tenant read, update,
    delete and reference attempts fail.
  - **RLS coverage**: every table with a `tenant_id` column has RLS enabled, forced, and a `tenant_isolation`
    policy.
  - **Pool leak**: a pooled connection never carries a previous request's tenant.
  - **API**: another tenant's ID returns `404`.

Acceptance:
- [ ] All four test groups pass; disabling a policy (temporary commit) makes the RLS coverage test fail — show in
      the PR.

### T5 — Public web shell and demo tenants
Deliver:
- Public app resolves the tenant per request (host → `/api/public/v1/site`) and renders tenant name, "Coming
  soon", footer; friendly pages for unknown host and unavailable tenant.
- DEMO seed (through Tenancy commands): `Kampala Metro Sports Association` (`kmsa`) and `Eastern Lakes Schools
  Sports Council` (`elssc`) from docs/product/pilot-customer.md, both `ACTIVE`, time zone `Africa/Kampala`,
  currency `UGX`, country `UG`.
- Playwright tests: both tenants render by host (or dev override), unknown host shows the not-found page.

Acceptance:
- [ ] `pnpm e2e` covers the three cases; screenshots at phone/tablet/desktop widths attached to the PR.

## Slice acceptance

- [ ] Two tenants with isolated data proven at application and database level.
- [ ] Tenant lifecycle with history and audit; public shell per host.
- [ ] Isolation harness documented in `.claude/rules/testing.md` so later slices extend it.
