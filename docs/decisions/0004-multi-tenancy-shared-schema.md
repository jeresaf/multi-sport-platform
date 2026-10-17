# ADR-0004 — Multi-tenancy: shared database and schema, `tenant_id`, EF Core filters plus PostgreSQL RLS

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The platform is leased to many customer organisations (tenants). The previous plan required "strict data
isolation … enforced below controllers" but did not say how. Options range from database-per-tenant to a
shared schema.

## Decision

- One PostgreSQL database; module schemas (ADR-0001); **every tenant-owned table has a non-null
  `tenant_id`** (UUID). Platform-global tables (reference data, sport definitions, formats, plans) have no
  `tenant_id`.
- **Tenant context** is resolved per request and never taken from a request body:
  - Admin API: authenticated user + selected tenant (header `X-Tenant-Id`) validated against the user's
    membership; platform staff may act on a tenant only through platform endpoints.
  - Public API/site: tenant resolved from host name (platform subdomain or verified custom domain).
  - Background jobs/consumers: tenant taken from the message envelope.
- **Layer 1 — application:** EF Core global query filters on every tenant-owned entity; a SaveChanges
  interceptor stamps `tenant_id` on insert and rejects writes whose `tenant_id` differs from the context.
- **Layer 2 — database:** PostgreSQL Row-Level Security policies on tenant-owned tables using
  `current_setting('app.tenant_id')`, which a connection interceptor sets every time a connection is opened
  (always set — empty when there is no tenant, which matches no rows). The application connects as a role that
  is subject to RLS (not the table owner, not a superuser). Platform-level cross-tenant operations use a
  separate, explicitly-audited code path.
- Unique constraints on tenant data include `tenant_id` (e.g. `(tenant_id, slug)`).
- A **generic isolation test harness** discovers all tenant-owned entities and proves cross-tenant reads and
  writes fail. Every new tenant-owned entity must be covered (CI).

## Alternatives considered

- **Database per tenant.** Strongest isolation; heavy operations (migrations × N, connection pools, backups).
  Revisit for an Enterprise/national tenant that legally requires it.
- **Schema per tenant.** Similar operational cost; complicates module schemas.
- **Application filters only.** Cheaper, but one missed filter leaks data; RLS is the safety net.

## Consequences

- Positive: two independent layers; isolation is testable in CI.
- Negative: RLS requires discipline in migrations (policy per new table) — enforced by a database rule
  (.claude/rules/database.md) and a test that fails if a tenant table lacks a policy.
- Platform analytics and support tooling must use explicit platform paths, never disable filters casually.

## Rules affected

AR-04, AR-05.

## Review trigger

A customer contract requiring physical separation, or measured noisy-neighbour performance issues.
