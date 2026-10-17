# ADR-0006 — Sport, format and ruleset templates are managed in the repository for v1; editions snapshot effective rules

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The previous plan specified three configuration systems (Sport Definition, Competition Format, Ruleset), each
with draft/publish/deprecate/archive lifecycles, editors, a visual stage builder and a visual progression-graph
editor — a large amount of admin UI before a single match can be scored. Tenants mostly need to *choose* a
sport and format and *adjust a few values* (points for a win, roster limits, period length, tiebreak order).

## Decision

For v1:
1. **Sport definitions, format definitions and ruleset templates are authored as files in the repository**,
   validated by JSON Schema in CI, and seeded into the `config` schema with `code`, `version` and a content
   hash. Changing a published version's content is forbidden — publish a new version instead (CI check).
2. Platform admins can **view** them in the admin app; there is no in-app editor yet.
3. Tenants **enable** sports they are entitled to, and in the competition wizard pick
   `sport variant + format + ruleset template`, then adjust only the **parameters the template exposes**
   (typed, validated, with defaults).
4. When an edition leaves `DRAFT`, its **effective rules are frozen** into an immutable snapshot
   (resolved JSON + hash + the exact versions used). Everything operational (fixtures, lineups, scoring,
   standings) reads the snapshot, never the live template.
5. Changing rules after freezing is a deliberate, audited `edition.rules.amend` action that creates a new
   snapshot version with an effective-from point and a reason.

Later (when justified): in-app definition editors and tenant-custom sports, gated by an Enterprise entitlement.

## Alternatives considered

- **Full editors from day one (pack).** Rejected: months of UI with no customer value yet.
- **Hard-coded C# sport classes.** Rejected: violates the owner's no-hard-coding requirement and makes
  versioning harder.

## Consequences

- Positive: immutability and reproducibility are achieved with git history + hashes + snapshots.
- Positive: adding a sport is a reviewed pull request with tests.
- Negative: a new sport needs a deploy; acceptable at this stage.

## Rules affected

AR-13, AR-14.

## Review trigger

More than a few tenants ask for sports/variants we don't ship, or federations require self-service rule
editing.
