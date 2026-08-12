# ADR-0004: Persist and replay mobile commands in order

- **Status:** Accepted
- **Date:** 2026-08-10

## Context

Scorers operate with intermittent connectivity. Losing, reordering, or silently
duplicating scoring commands would corrupt the sporting record.

## Decision

Persist the exact API command in Drift before replay, with a stable UUID and
monotonic local sequence. Replay serially. Stop after a transient failure, mark
conflicts/rejections for review, and rely on server idempotency keys and unique
constraints to make retries safe.

## Consequences

- Process restarts and connectivity changes do not lose queued work.
- Later commands cannot overtake an unavailable earlier command.
- Conflict review is an explicit user/support workflow.
- Schema/API compatibility must be maintained for queued command payloads.

## Alternatives

In-memory retry was rejected because process termination loses data. Parallel
replay was rejected because order affects scores. Last-write-wins was rejected
because it conceals conflicting sporting records.

