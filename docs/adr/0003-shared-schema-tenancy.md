# ADR-0003: Use explicit shared-schema tenant scoping

- **Status:** Accepted
- **Date:** 2026-08-10

## Context

Most business data belongs to one organization. The system needs strong
isolation while retaining one operational database and platform-level support.

## Decision

Store tenant-owned records in a shared PostgreSQL schema with `TenantId`.
Resolve tenant context from authenticated claims or explicit supported headers,
scope service/repository queries, verify referenced-resource ownership, qualify
SignalR groups, and require workers to establish tenant scope explicitly. Test a
cross-tenant operation matrix against PostgreSQL.

## Consequences

- Provisioning, backup, reporting, and upgrades remain centralized.
- Every new tenant-owned query/write must visibly enforce tenant scope.
- The current architecture does not claim universal EF filters or database RLS;
  adding RLS would be a separate defense-in-depth ADR and migration.

## Alternatives

Database-per-tenant was rejected for current operational cost. Subdomain-only
resolution was rejected because native mobile and direct API clients need an
explicit tenant mechanism. Relying only on controller checks was rejected because
repositories, hubs, and workers also access tenant data.

