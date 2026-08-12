# ADR-0005: Publish SignalR updates after persistence

- **Status:** Accepted
- **Date:** 2026-08-10

## Context

Spectators and operators need timely updates, but reconnects and delivery gaps
are normal and a broadcast cannot be the authoritative sporting record.

## Decision

Persist commands through REST/application services first, then publish SignalR
notifications to ownership-checked tenant/resource groups. Send a current
snapshot on contest join and require reconnecting clients to reconcile from
REST/snapshot state. Permit anonymous group joins only for public tenant contests.

## Consequences

- Clients never treat an uncommitted broadcast as authority.
- SignalR delivery is low latency but not guaranteed event replay.
- Group authorization and tenant qualification are security boundaries.

## Alternatives

Broadcast-before-save was rejected because clients could observe rolled-back
state. A durable message broker was deferred until multiple backend processes or
external consumers require guaranteed integration-event delivery.

