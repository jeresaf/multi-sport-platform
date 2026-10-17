# Definition of Ready and Definition of Done

## Ready (before a task starts)

- [ ] Task listed in a slice spec with goal, scope, out-of-scope and acceptance criteria
- [ ] Owning module(s) identified (docs/architecture/modules.md); no ownership ambiguity
- [ ] Action codes, error codes and events named (or the task is to define them)
- [ ] State transitions referenced from docs/domain/state-machines.md
- [ ] Dependencies on earlier tasks merged
- [ ] Open questions answered, or a safe default agreed and recorded

If any box is empty, ask the owner before planning.

## Done — backend task

- [ ] Domain rules implemented in the owning module; no cross-module data access
- [ ] Every new endpoint declares an action code; authorization tested (allowed + denied)
- [ ] Unit tests for rules; integration tests on real PostgreSQL for handlers/endpoints
- [ ] New tenant-owned entities covered by the isolation harness; RLS policy in the migration
- [ ] Audit entries for audit-required actions (with reason where required)
- [ ] Integration events published through the outbox where other modules react
- [ ] Optimistic concurrency on mutable aggregates; idempotency where required
- [ ] Errors use stable codes; OpenAPI documents responses and codes
- [ ] Migrations reviewed (naming, constraints, indexes); seeds updated through commands
- [ ] No PII in logs/analytics/audit metadata; public DTOs only `PUBLIC` fields
- [ ] Architecture tests green; generated catalogues and OpenAPI regenerated
- [ ] Docs updated if behaviour or contracts changed

## Done — UI task

- [ ] Uses the generated API client; no hand-written duplicate types
- [ ] Loading, empty and error states (error shows code + correlation ID)
- [ ] Respects capabilities (hidden/disabled with reason)
- [ ] Phone, tablet and desktop layouts checked (screenshots in PR)
- [ ] Accessibility: labels, focus, keyboard, touch targets, contrast
- [ ] Long names, missing images and TBD participants handled
- [ ] Light/dark themes look right (tokens only, no hard-coded colours)
- [ ] Realtime behaviour tested where applicable (version gaps, reconnect)
- [ ] Playwright test for the user flow

## Done — slice

- [ ] All tasks merged; slice acceptance criteria demonstrated (owner demo)
- [ ] Roadmap status and PROGRESS.md updated; tech debt logged
- [ ] Reviewer's slice review done; next slice spec ready
