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
- **Every new tenant-owned entity:** confirm the isolation harness covers it (cross-tenant read/update/delete/
  reference all fail) and the RLS coverage test passes.
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
  to code under test (it bypasses RLS). Migrations history tables are not reset. Pass `Ct`
  (`TestContext.Current.CancellationToken`) to async calls.
- **Never** delete, skip or weaken a failing test to get green. Fix the cause or ask the owner.
