# S02 — Tenancy core and isolation

> Amended by the S01 slice review (2026-10-18) to match what S01 actually built. It replaces the kit's version.
> Changes from the kit's version: the tasks are reordered so no tenant-owned table reaches `main` without RLS
> (enforcement is now T2, before the first tenant-owned business table in T4); the decisions below (D1–D10) are
> written out; the S01 follow-ups assigned to S02 are folded into the tasks ("Carried over from S01").

**Goal:** tenants exist, every request knows its tenant, and tenant data is isolated in the application **and**
the database, proven by tests that every later slice reuses.
**Modules touched:** Tenancy & Commercial (new module `Tenancy`, schema `tenancy`), Platform, Host, public web.
**Depends on:** S01.
**Demo:** the platform API creates, activates, suspends and restores tenants, with status history and audit
entries; `kmsa.localhost:3000` and `elssc.localhost:3000` show different tenants; an unknown host shows a friendly
page; the isolation and RLS tests prove cross-tenant access fails at both layers.

Read first: ADR-0004, docs/architecture/multi-tenancy.md, docs/domain/state-machines.md (Tenant, Access mode),
docs/architecture/authorization.md ("Decision formula"), docs/architecture/events-and-jobs.md ("Audit as built",
Q15), docs/product/pilot-customer.md.

## What S01 gives you (use it, don't rebuild it)

- `ITenantOwned` (BuildingBlocks `Domain`, get-only `TenantId`); `ITenantContext` + scoped, settable
  `TenantContext` (BuildingBlocks `Tenancy`); `RequestContextEnricher` already logs `TenantId` when it is set.
- `ModuleDbContext`, `MessagingInterceptor` → `AuditColumnsInterceptor` (order matters), `AddModuleDbContext`,
  the three connection names (`App` = `sp_app`, `Migrator` = `sp_migrator`, `Platform` = `sp_platform`, BYPASSRLS).
  Nothing uses `Platform` yet: S02 is its first user.
- Migration helpers `ReadOnlyForRuntimeRoles`, `AppendOnly` and the grants test (`TableAccessPolicies`).
- Actions (`ActionDefinition`, `.RequireAction`, `.AllowPublic`, fail-closed endpoint check), the authorization
  middleware (after routing) and `IAuthorizationEvaluator` (Host default: deny all; tests: `TestAuthorizationEvaluator`).
- `AuditScope` + `AuditMiddleware` (before authorization) + `AuditRecorded.V1` through each module's outbox.
- Outbox/inbox per module schema (no RLS, Q15); consumers run under the event envelope's tenant and require
  `ITenantContext` to resolve to the scoped `TenantContext`.
- FluentValidation request validation (`.ValidateRequest<T>(error)`, `errors: [{field, code}]`).
- Seed framework (`ISeeder`, categories, `SeedContext`, `DeterministicIds`), reference data and its public lists.
- `CacheableJson` for cacheable public reads (responses must be the same for every caller of that URL and host).
- Test harness: `DatabaseTest`, `PostgresFixture` (Respawn on a superuser connection only the fixture holds),
  `TestApp`, `TestActor`, `TestTenantContext` (to be retired by T3).

## Decisions (reviewer defaults; confirm or change in each task's plan)

- **D1 — Tenant context is set once per scope.** `TenantContext.Set` throws if a *different* tenant is already
  set in the scope (setting the same tenant again is a no-op). It must be set before the first connection that reads
  or writes tenant data opens, because the RLS value is written when a connection opens; a read of a global table
  only (e.g. tenant creation's slug check) may come first (amended by the owner, 2026-10-18, #30 R2). If the handler
  uses an explicit transaction, set the tenant before the transaction begins: the connection stays open until commit
  (#31 R1).
- **D2 — Platform work on one tenant runs under that tenant.** Platform endpoints under
  `/api/platform/v1/tenants/{tenantId}/…` set `ITenantContext` to the route's tenant after the route is matched
  (the platform action is still authorized by the middleware). Tenant creation sets the context to the new
  tenant before its first save. So RLS applies, and audit entries get the target tenant without extra code.
- **D3 — Cross-tenant reads only through `sp_platform`, in named platform services.** The platform audit query
  (`GET /api/platform/v1/audit-entries`) and the audit writers move to the `Platform` connection (see T2).
  `IgnoreQueryFilters()` stays forbidden in feature code; the named platform services are the reviewed exceptions
  (multi-tenancy.md). The outbox processor and diagnostics stay on `App` (Q15: outbox/inbox have no RLS).
- **D4 — The RLS coverage test has an explicit exception list with reasons:** `*.outbox_messages` and
  `*.inbox_messages` (nullable envelope `tenant_id`, no RLS by owner decision Q15). Any other table with a
  `tenant_id` column without RLS fails the test.
- **D5 — Actor.** S02 adds a per-request `ICurrentActor` built from the authenticated principal. Only the test
  authentication scheme (environment `Testing`) authenticates in S02. Unauthenticated requests keep `SystemActor`
  until S03 adds the anonymous actor (S03 follow-up #23 R1), so the audit `actor_kind` constraint is not hit.
- **D6 — Public tenant resolution is opt-in per endpoint.** Public endpoints that serve tenant data declare it
  (e.g. `.RequireTenant()` metadata on the endpoint or group); the reference lists stay tenant-free and keep
  working on any host.
- **D7 — Where the public tenant comes from.** The request host, honouring `X-Forwarded-Host` only from configured
  proxies (ASP.NET Core forwarded headers with known proxies/networks; the Next.js server is the proxy in dev).
  This is safe because public data is public. In Development only, `X-Tenant-Slug` or `?tenant=` may override.
  Admin requests never take the tenant from the host or any header other than a membership-validated
  `X-Tenant-Id`.
- **D8 — Pipeline order:** correlation and logging → routing → **audit** → **tenant resolution** → **authorization**
  (step 1 access mode, then the evaluator) → endpoint. Audit runs before tenant resolution so a refused tenant
  (`AUTH_TENANT_FORBIDDEN`, `TENANT_BLOCKED`) on an audited action is audited as `DENIED`. Update
  docs/architecture/authorization.md to match.
- **D9 — Access mode is status-only in S02:** `PROVISIONING`, `ACTIVE` → `FULL`; `SUSPENDED`, `ARCHIVED` →
  `BLOCKED` (state-machines.md; subscriptions arrive in S04). `PROVISIONING` keeps the public site hidden.
- **D10 — Tenant events carry IDs and `PUBLIC` fields only** (ADR-0015, Q15 condition): `TenantCreated.V1`
  (`tenantId`, `slug`, `status`), `TenantStatusChanged.V1` (`tenantId`, `from`, `to`). They are the first entries
  in `docs/generated/events.md`.

## Out of scope

- Users and login (S03). Tests use a test authentication scheme that supplies actor ID and tenant memberships as
  claims; production has no sign-in yet, so admin endpoints stay denied.
- The anonymous actor kind and its audit migration (S03).
- Plans and subscriptions (S04): access mode is status-only (D9).
- Custom domains beyond the data model (verification flow comes with Branding/PRO).
- A tenant-facing audit view (the RLS policy is in place for it; the endpoint comes later).

## Tasks

### T1 — Tenancy module, tenants and lifecycle
Deliver:
- New module `Tenancy` (`SportsPlatform.Modules.Tenancy` + `.Contracts`), schema `tenancy`, registered in
  `ModuleRegistry`; architecture tests updated (the scan finds the new module and its Contracts).
- `Platform.Contracts`: `IReferenceData` (is this an active country / time zone / currency / language code),
  backed by the reference tables (S01 follow-up). Tenancy validates through it, never by reading Platform tables.
- `tenancy.tenants` (platform-managed, global, no RLS): `id, slug, legal_name, display_name, country_code,
  time_zone, default_locale, currency_code, status, public_enabled`, audit columns, `version` (`xmin`).
  `tenancy.tenant_status_history` (append-only: `AppendOnly`): `tenant_id, from_status, to_status, reason,
  actor_kind, actor_id, occurred_at`. Both declared in `TableAccessPolicies`.
- The tenant state machine exactly as in state-machines.md, including `unarchive` (platform, reason).
- Actions (PLATFORM scope): `tenant.create`, `tenant.update`, `tenant.activate`, `tenant.suspend` (reason),
  `tenant.restore`, `tenant.archive` (reason), `tenant.unarchive` (reason), `tenant.read`. Audit:
  `REQUIRED_WITH_REASON` where a reason is required, `REQUIRED` otherwise. Endpoints under
  `/api/platform/v1/tenants` (`GET` list and detail, `POST` create, `PATCH` profile, `POST …/{tenantId}/<transition>`).
- Validation: slug `^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$` (3–40, no leading/trailing hyphen), reserved words (`www`,
  `api`, `admin`, `app`, `platform`, `static`, `assets`, `mail`, `status`, `docs`), unique for ever (an archived
  tenant's slug is never reused); country, time zone, currency and locale through `IReferenceData`.
- Errors (`TenancyErrors`): `TENANT_INVALID_TRANSITION` (409), `TENANT_SLUG_TAKEN` (409), `TENANT_NOT_FOUND` (404),
  `TENANT_CONCURRENCY_CONFLICT` (409); request-shape failures reuse `VALIDATION_*` with `WithErrorCode` only for
  declared codes (#22 N3: add that rule to `.claude/rules/backend.md` here).
- Integration events `TenantCreated.V1`, `TenantStatusChanged.V1` (D10), published through the Tenancy outbox under
  the tenant's context (D2).

Acceptance:
- [x] Every transition in the state machine works; every other transition returns `409 TENANT_INVALID_TRANSITION`;
      a missing reason returns `400 VALIDATION_REASON_REQUIRED`.
- [x] Each transition writes one status-history row and one audit entry whose `tenant_id` is the target tenant (D2).
- [x] Allowed and denied tests for every new action.
- [x] `events.md`/`events.json` list both events with their payloads.

### T2 — Isolation enforcement (application + database)
Deliver:
- **Application layer:** a global query filter convention in `ModuleDbContext` for every `ITenantOwned` entity
  (`TenantId == current tenant`); a `SaveChanges` interceptor that stamps `TenantId` on added entities through
  `entry.Property(...).CurrentValue` (no public setter, #13) and throws when an added or modified entity's tenant
  differs from the context, or when there is no tenant.
- **Database layer:** a `DbConnectionInterceptor` that runs `set_config('app.tenant_id', <tenant or ''>, false)` on
  every connection open (multi-tenancy.md); migration helper `EnableTenantRls(schema, table)` (enable + force +
  `tenant_isolation` policy with `USING` and `WITH CHECK` on `NULLIF(current_setting('app.tenant_id', true), '')::uuid`).
- `TenantContext.Set` once per scope (D1).
- **`audit.audit_entries` gets RLS** (S01 follow-up): `EnableTenantRls`; rows with no tenant are visible only
  through platform services. The audit writers (`AuditLog`, `AuditRecordedConsumer`) and the platform audit query
  (`ListAuditEntriesHandler`) use the `Platform` connection (D3). The plan names the mechanism (for example a
  Platform-internal audit context on `Platform` that is not migrated and maps the same tables) and how the consumer
  still writes its inbox row in the same transaction.
- Audit indexes reworked tenant-first, and the entity index becomes `(entity_type, entity_id, occurred_at)`
  (#24 R2); a migration, not an edit of the T6 migration.
- `AuditScope.Describe(…, targetTenantId:)` for platform reads that are filtered to one tenant but not under a
  tenant route (the audit query with `?tenantId=`): the entry gets that tenant (S01-T6b follow-up).
- **Tests** in `SportsPlatform.IntegrationTests/Isolation`, reused by every later slice:
  - **Isolation harness:** discovers every `ITenantOwned` entity type in every registered `ModuleDbContext`,
    creates a row in tenant A, then as tenant B asserts it is not visible, can't be updated, can't be deleted and
    can't be referenced. Each entity type needs a registered sample builder; an entity without one **fails** the
    harness (so nothing is skipped silently). Until T4 the harness runs on a test-only `ITenantOwned` entity in the
    `conventions_test` schema.
  - **RLS coverage:** every table with a `tenant_id` column has RLS enabled and forced and a `tenant_isolation`
    policy, except the documented list (D4).
  - **Pool leak:** a pooled connection never carries a previous scope's tenant (open with tenant A, return it,
    reopen without a tenant → `''`, no rows).
  - **Audit under RLS:** a tenant entry is invisible to `sp_app` under another tenant; the platform query still
    sees every tenant; a direct write (denial) and an outbox write (re-drive) both still land.
  - **Consumers under RLS:** a consumer writing a tenant-owned row runs under the envelope's tenant and succeeds;
    the same write under another tenant fails.
- Docs: multi-tenancy.md (as built), `.claude/rules/testing.md` (how to add a sample builder to the harness).

Acceptance:
- [x] All test groups pass. A temporary commit that drops one policy makes the RLS coverage test fail; link the
      failing CI run in the PR.
- [x] `check-roles.sh` and the grants test still pass; nothing in feature code uses `IgnoreQueryFilters()`.

### T3 — Tenant context and resolution
> Two PRs (owner, 2026-10-19, Q17): **T3a** admin and platform resolution, `ICurrentActor`, access mode, AR-21;
> **T3b** `tenancy.tenant_domains` (moved here from T4), forwarded headers, public resolution and its cache, the dev
> override and `GET /api/public/v1/site`.

Deliver:
- Tenancy Contracts: `ITenantDirectory` (by ID, slug, host), `ITenantAccess` (status-only access mode, D9).
- Host tenant-resolution middleware in the D8 position:
  - **Admin:** `X-Tenant-Id` validated against the actor's memberships through `ITenantMembershipChecker`
    (BuildingBlocks `Tenancy` interface; Host default: no memberships; tests: reads membership claims; S03
    replaces it). Missing or not a member → `403 AUTH_TENANT_FORBIDDEN`.
  - **Platform:** tenant routes set the route's tenant (D2); unknown tenant → `404 TENANT_NOT_FOUND`.
  - **Public:** host → `tenancy.tenant_domains` (T3b creates it: global, no RLS; `host` unique, type
    `PLATFORM_SUBDOMAIN | CUSTOM`, `is_primary`, status; creating a tenant creates `<slug>.<Platform:BaseDomain>`, dev
    default `localhost`, Q10 sets the real one; existing tenants are backfilled) (D6, D7) through `ITenantDirectory`,
    with a short-lived in-memory cache (at most 60 s) invalidated in-process on domain or status change. Unknown host → `404 TENANT_NOT_FOUND`;
    a known host whose tenant is hidden (`PROVISIONING`, `public_enabled = false`) or `BLOCKED` →
    `403 TENANT_UNAVAILABLE` (the host is public knowledge; the body says nothing more), so the public web can show
    "not found" and "unavailable" as different pages.
  - **Jobs/consumers:** unchanged (envelope tenant).
- Access mode in the authorization middleware, step 1 (authorization.md): an admin request on a `BLOCKED` tenant →
  `403 TENANT_BLOCKED`.
- Errors: `AUTH_TENANT_FORBIDDEN` (403), `TENANT_BLOCKED` (403), `TENANT_UNAVAILABLE` (403); `TENANT_NOT_FOUND`
  from T1.
- Per-request `ICurrentActor` from the authenticated principal (D5); the test authentication scheme (Testing only)
  supplies actor ID and memberships. Retire `TestTenantContext`; tests choose the tenant through headers, claims
  or `TenantContext.Set` in a scope.
- `GET /api/public/v1/site` (requires a public tenant): display name, primary domain, public flags — `PUBLIC`
  fields only. Cacheable with `CacheableJson`; responses differ per host, which shared caches key on.
- AR-21 architecture test (DTOs are not entities): no endpoint's documented response type derives from `Entity`
  (the first tenant DTO arrives here).

Acceptance:
- [ ] Wrong or missing tenant header → `403 AUTH_TENANT_FORBIDDEN`; unknown host → `404 TENANT_NOT_FOUND`;
      blocked tenant → `403 TENANT_BLOCKED` (admin); hidden or blocked tenant on its host → `403 TENANT_UNAVAILABLE`
      (public); each audited when the action is audited.
- [ ] The reference lists still answer on any host (D6).
- [ ] A forwarded host from an unknown proxy is ignored (D7); the dev override works only in Development.

### T4 — Settings
> `tenancy.tenant_domains` moved to T3b (owner, 2026-10-19, Q17).

Deliver:
- `tenancy.tenant_settings` — the **first tenant-owned business table** (`ITenantOwned`, `EnableTenantRls`):
  typed, allow-listed keys: time zone, locale, currency, public visibility, `starting_soon_minutes`,
  `minor_registration_consent` (`REQUIRED | RECOMMENDED | OFF`, default `RECOMMENDED`, Q11). Created with defaults
  in the tenant-creation transaction under the new tenant's context (D2).
- Admin endpoints `GET`/`PATCH /api/admin/v1/tenant/settings` (TENANT actions `tenant.settings.read`,
  `tenant.settings.update`; audit `REQUIRED`). Values validated through `IReferenceData`.
- Sample builder for `TenantSettings` in the isolation harness.

Acceptance:
- [ ] Unknown keys and wrongly typed values are rejected with `errors` entries naming the field.
- [ ] The isolation harness covers `tenant_settings` (and fails if its builder is removed); the RLS coverage test
      sees its policy.
- [ ] Cross-tenant API test: tenant B's admin can't read or change tenant A's settings (`403` for the header,
      no data either way).

### T5 — Public web shell and demo tenants
Deliver:
- The public app resolves the tenant per request (host → `/api/public/v1/site`, sending the host as
  `X-Forwarded-Host`, D7) and renders the tenant's name, "Coming soon" and a footer; friendly pages for an unknown
  host and an unavailable tenant.
- Seeding support (S01-T7a follow-up #25 R3, owner: demo seeding is audited): `SeedContext` can run commands as a
  tenant (sets `TenantContext` once in the seeder's scope, D1) and begins an audit scope with source `SYSTEM` for
  the command's action.
- DEMO seeder (through Tenancy commands, never raw inserts): Kampala Metro Sports Association (`kmsa`) and Eastern
  Lakes Schools Sports Council (`elssc`) from docs/product/pilot-customer.md, both `ACTIVE`, `Africa/Kampala`, `UGX`,
  `UG`; deterministic IDs (`tenancy.tenant:<slug>`); idempotent by slug.
- Playwright: both tenants render by host (or the dev override); an unknown host shows the not-found page.

Acceptance:
- [ ] `seed --category DEMO` twice: the second run changes nothing; each created tenant has audit entries with
      source `SYSTEM`.
- [ ] `pnpm e2e` covers the three cases; screenshots at phone, tablet and desktop widths in the PR.

## Carried over from S01 (slice review 2026-10-18)

Each is assigned above; remove it from PROGRESS.md "Tech debt" when its task merges.
- `ITenantOwned.TenantId` stays get-only; the interceptor sets it through the change tracker (#13) — T2.
- RLS on `audit.audit_entries`, tenant-first audit indexes, entity index with `occurred_at` (#24 R2) — T2.
- Target tenant on platform reads (`AuditScope`) — T2 (and D2 for tenant routes).
- `IReferenceData` in Platform Contracts — T1.
- `SeedContext` runs commands as a tenant and audits demo seeding as `SYSTEM` (#25 R3, owner: yes) — T5.
- Consumers keep a settable `ITenantContext` (Q15) — T2 test "Consumers under RLS".
- `WithErrorCode` only with declared codes, rule in backend.md (#22 N3) — T1.
- AR-21 architecture test — T3.

## Slice acceptance

- [ ] Two tenants with isolated data, proven at the application and database levels (harness, RLS coverage, pool
      leak, API tests green in CI).
- [ ] Tenant lifecycle with status history and audit; public shell per host.
- [ ] The isolation harness and the RLS exception list are documented in `.claude/rules/testing.md` so later slices
      extend them.
- [ ] The S01 carry-overs above are done and removed from PROGRESS.md "Tech debt / follow-ups".

## Implementation notes

(The builder adds notes here as tasks merge.)

**T1 (#30 T1a, T1b).** Delivered in two PRs (owner). Tenants are created in `PROVISIONING` with a first history row
(`from_status` NULL); `publicEnabled` defaults to false; `default_locale` is `ll` or `ll-CC`. Changes take an optional
body `version` and/or `If-Match` (strong comparison, `*`); each one sent must be current, else
`TENANT_CONCURRENCY_CONFLICT`. Body fields that fail validation are `VALIDATION_INVALID_REQUEST` (new shared code);
bodies reject unknown members. The tenant context is set by the Tenancy handlers until T3's route middleware (D2); a
taken slug is audited without a tenant. `tenant_status_history` has `tenant_id` and gets RLS in T2.

**T2a (#32).** T2 is two PRs (owner): T2a enforcement and harness, T2b audit under RLS and the RLS coverage
test. The query filter is named `TenantIsolation`. Foreign keys between tenant-owned tables are composite
`(tenant_id, x_id) → (tenant_id, id)`; `ModuleDbContext` refuses single-column ones (owner). The harness runs on
`conventions_test.tenant_probes`/`tenant_probe_notes`, `messaging_test.consumed_notes` and
`tenancy.tenant_status_history`; it also checks that a row can't be copied into another tenant. Owner contexts (seeds
on `Migrator`) have no tenant, so they can't write tenant-owned rows.

**T2b.** The audit trail is a platform service: `PlatformAuditDbContext` (registered with
`AddPlatformServiceDbContext`, connection `Platform`, never migrated) maps `audit_entries` and, from the base,
Platform's outbox and inbox, so `AuditRecordedConsumer` commits its inbox row with the entry; `AuditLog` and the
platform query use it too, and an architecture test allows no other users. `audit_entries` has `EnableTenantRls`;
indexes `(tenant_id, action_code, occurred_at)`, `(tenant_id, actor_id, occurred_at)` and
`(entity_type, entity_id, occurred_at)` replace the action, actor and entity ones (entity, actor and correlation IDs are
global). `AuditScope.Describe(targetTenantId:)`: the scope's tenant wins, a different target throws. The RLS coverage
test also refuses a second permissive policy and stale exceptions; its only exception is `*.outbox_messages`
(`inbox_messages` has no `tenant_id`, D4 named it). `IgnoreQueryFilters(` is refused under `backend/src` by an
architecture test. Under `FORCE` RLS the owner sees an audit entry only with `app.tenant_id` set.

**T3a.** Tenant actions (the admin API) resolve `X-Tenant-Id` with membership; platform endpoints opt in with
`.RunsUnderRouteTenant()` (a platform action and the route parameter, checked at startup) and every other platform
endpoint runs without a tenant. Step 1 calls `ITenantAccess.CheckAdminAccessAsync`, which returns `TENANT_BLOCKED` or
`TENANT_NOT_FOUND` from `TenancyErrors` (errors are declared in module assemblies, not Contracts); no resolved tenant
is `AUTH_TENANT_FORBIDDEN`. `ITenantDirectory` has `GetByIdAsync` (a `Result`) and `FindBySlugAsync`; T3b adds the
host lookup. The actor is a scoped `ActorContext` set from the principal's `sub`; seeds stamp the system actor. A
404 for an unknown route tenant is audited without a tenant or entity (the handler never runs). Acceptance items
for the admin header and `TENANT_BLOCKED` are covered by `TenantResolutionTests`; the host items are T3b's. The
startup check also requires `.RunsUnderRouteTenant()` on any platform route with `{tenantId}`. Known trade-off of D8:
the route tenant is looked up before authorization, so a caller without the action gets 404 for an unknown tenant and
403 for a known one (tenant IDs are UUIDv7, and the 404 is audited as `FAILURE`).
