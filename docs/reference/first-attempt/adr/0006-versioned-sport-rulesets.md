# ADR-0006: Pin competitions and contests to ruleset versions

- **Status:** Accepted
- **Date:** 2026-08-10

## Context

Sport definitions evolve, but an active or completed competition must retain the
segments, event validation, result methods, and ranking rules under which it ran.

## Decision

Create immutable-in-use `SportDefinitionVersion` records, publish them, and pin
their IDs to competitions/contests. Store generic definition components as JSON
under a typed version identity. Apply sport-specific services for creation and
validation.

## Consequences

- Historical contests remain reproducible after new rules are published.
- Corrections and projection rebuilds can use the original ruleset identity.
- Published versions must not be edited in place; changes require a new version.
- JSON definition evolution requires validation and compatibility tests.

## Alternatives

One mutable definition per sport was rejected because it rewrites history.
Hard-coded sport subclasses alone were rejected because routine rule/display
variation would require deployments and duplicate shared behavior.

