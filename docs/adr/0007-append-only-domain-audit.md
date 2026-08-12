# ADR-0007: Preserve business audit history append-only

- **Status:** Accepted
- **Date:** 2026-08-10

## Context

Scoring corrections, approvals, access changes, subscription actions, privacy
decisions, and support operations need attributable history that cannot be
silently rewritten through the application.

## Decision

Record domain-meaningful `AuditEntry` rows with tenant, actor, action, target,
before/after values, reason, time, and request context. Reject update/delete in
EF and enforce append-only behavior with a PostgreSQL trigger in staging and
production. Retain original/proposed correction data and audit decisions.

## Consequences

- Investigations and support workflows have durable evidence.
- Corrections append compensating history rather than changing old entries.
- Sensitive values must be minimized/redacted before recording.
- Audit growth requires retention/storage planning without mutating protected
  sporting or security history.

## Alternatives

Ordinary mutable logs were rejected because administrators could alter history.
Logging every EF property change automatically was rejected because it lacks
business meaning and risks capturing secrets; explicit domain audit is augmented
only for selected persistence-level invariants.

