---
paths:
  - "backend/tests/**"
  - "web/e2e/**"
  - "web/**/*.test.ts"
  - "web/**/*.test.tsx"
  - "web/**/*.spec.ts"
---

# Testing rules

- **Shape:** many fast unit tests (domain rules, engine scenarios, generators) → integration tests for every
  endpoint/handler on real PostgreSQL (Testcontainers, Respawn) → a few Playwright flows for critical journeys.
- **Every new action:** an allowed test and a denied test (permission; entitlement/access mode where relevant).
- **Every new tenant-owned entity:** add an isolation sample (below) and confirm the RLS coverage test passes.
- **Isolation harness** (`SportsPlatform.IntegrationTests/Isolation/TenantIsolationTests`): finds every
  `ITenantOwned` entity of every module DbContext and, for each, creates a row as tenant A and proves that tenant B
  can't read it (EF and raw SQL as `sp_app`), change or delete it (interceptor; RLS matches no row, or no grant on
  append-only tables), copy it into A (RLS `WITH CHECK`) or reference it (composite foreign key). A type without a
  sample fails `Every_tenant_owned_entity_has_a_sample`. To add one: a class implementing `IIsolationSample` in
  `Isolation/IsolationSamples.cs`, listed in `IsolationSamples.All`. `Add(context)` adds a new entity and the parents
  it needs (leave `TenantId` empty unless the entity takes it from its parent); `Referencing(foreignKey, id)` returns
  a new entity pointing at `id` for each tenant-owned foreign key **onto** another type, else null. Discovery uses
  the `ModuleDatabase` registrations (`AddModuleDbContext`): a `ModuleDbContext` without one fails
  `ModuleDatabaseRegistrationTests` unless it is listed there with a reason.
- **RLS coverage** (`Isolation/RlsCoverageTests`): every table with a `tenant_id` column needs `EnableTenantRls` in
  its migration (test schemas included). A table that must not have RLS goes in the test's exception list with a
  reason, agreed with the owner; a stale exception fails the test. Exceptions: `outbox_messages` (Q15) and
  `tenant_domains` (global host lookup, S02-T3b).
- **Actor and tenant over HTTP:** `TestApp` and `HostOnlyApp` register the test authentication scheme
  (`Infrastructure/TestAuthentication`): `client.AsUser(userId, memberOf: tenantIds)` sends `X-Test-User` and
  `X-Test-Tenants`, so the request runs as that user and passes the membership check for `X-Tenant-Id`. Without it a
  request is the system actor. Outside HTTP, set `scope.ServiceProvider.GetRequiredService<ActorContext>().Set(new
  UserActor(id))`. Admin requests also send `X-Tenant-Id` (pattern: `Tenancy/TenantSettingsTests`). For pipeline
  tests, `Tenancy/AdminTestApp` runs the Host's services and pipeline (`SportsPlatformApp.AddServices`/`UsePipeline`)
  with a test-only audited tenant action.
- **Public requests by host:** `App.Server.GetAsync(path, host, from, headers)` (`Tenancy/PublicRequests`) sets the
  host and remote address. `Tenancy/PublicTestApp` runs the whole Host in another environment (Development for the
  override) and trusts `PublicRequests.Proxy` as a forwarding proxy. `TenantTestData.SaveTenantAsync` adds the
  tenant's `<slug>.localhost` domain and default settings (`publicEnabled` as given); `DatabaseTest` clears the host
  cache before each test.
- **Tenant in tests:** set the scope's tenant with `scope.ServiceProvider.GetRequiredService<TenantContext>().Set(id)`
  before the first query or save; without one, tenant-owned queries return nothing and saves throw. Read across
  tenants only as `sp_platform` (`QueryAsync(DatabaseRoles.Platform, …)`). Audit entries are under RLS: read them
  with `AuditTestData.EntriesAsync` (`PlatformAuditDbContext`, `sp_platform`); as the owner, set `app.tenant_id` first.
- **Every sport rule:** a passing and a rejecting engine scenario (YAML under
  `backend/tests/SportsPlatform.UnitTests/Engines/Scenarios/<variant>/`), written so a non-developer can read it.
- **Deterministic:** fake `TimeProvider`, fixed random seeds, no `Thread.Sleep`/arbitrary waits; Playwright waits
  on visible UI state.
- **Realistic data:** fictional Ugandan/East African names and places; builders/factories per module; no shared
  mutable fixtures between tests.
- Don't mock the database in integration tests. Mock only external services (email → Mailpit/test double).
- Assert error `code`s, not message text.
- **Database tests** derive from `DatabaseTest` (`SportsPlatform.IntegrationTests/Infrastructure`): one
  Testcontainers PostgreSQL per run (compose's image tag + `01-roles.sh`), migrated as `sp_migrator`; the app
  (`TestApp`, environment `Testing`, `App.Clock` is a `FakeTimeProvider`) runs as `sp_app`. Each test starts
  with a Respawn reset on a separate superuser connection that only the fixture holds — never hand the superuser
  to code under test (it bypasses RLS). The reset sets `session_replication_role = replica` on an unpooled
  connection so the append-only triggers don't block its TRUNCATE. Migrations history tables are not reset.
  Pass `Ct` (`TestContext.Current.CancellationToken`) to async calls.
- **Never** delete, skip or weaken a failing test to get green. Fix the cause or ask the owner.
