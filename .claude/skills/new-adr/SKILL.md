---
name: new-adr
description: Draft a new Architecture Decision Record with status Proposed, numbered and indexed, for the owner to accept.
argument-hint: "<decision title>"
disable-model-invocation: true
allowed-tools: Read Grep Glob
---

Create an ADR titled: $ARGUMENTS

1. Find the highest number in `docs/decisions/` and use the next one (4 digits).
2. Create `docs/decisions/NNNN-<kebab-case-title>.md` from `docs/decisions/template.md` with
   `**Status:** Proposed` and today's date.
3. Fill Context, Decision (precise enough to implement without interpretation), Alternatives considered,
   Consequences, Rules affected (AR-xx) and Review trigger, using what was discussed with the owner.
4. If it replaces an existing ADR, say so in Context — the old ADR's status line changes to
   `Superseded by ADR-NNNN` only after this one is Accepted.
5. Add a row to the index in `docs/decisions/README.md`.
6. Tell the owner the ADR needs acceptance (and the reviewer's comments). Don't implement a Proposed decision.
