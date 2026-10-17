# Definition of Ready / Definition of Done

# Definition of Ready — Module Feature

A task is ready when:

- Owning module identified
- Specification section identified
- Source-of-truth entity identified
- Upstream dependencies identified
- Action code identified
- Permission identified
- Entitlement identified if applicable
- State transition/business rule defined
- Audit requirement known
- API contract direction known
- Error codes defined
- Acceptance criteria defined
- No unresolved ownership ambiguity

If these are missing, refine the specification before implementation.

# Definition of Done — Backend Feature

- Domain rule implemented
- Unit tests pass
- Integration tests pass
- Tenant isolation covered
- Permission/entitlement covered
- Migration added if needed
- Concurrency handled
- Audit emitted if required
- Domain event emitted if required
- API documented
- Error contract follows standard
- Seed/demo scenario updated if needed
- Architecture tests pass
- No cross-module data mutation
- Docs updated

# Definition of Done — UI Feature

- Uses canonical API contract
- Loading/empty/error states
- Permission/entitlement state
- Responsive mobile/tablet/desktop where applicable
- Accessibility labels/focus/touch targets
- Long-name/missing-image state
- Dark/light themes where applicable
- Tenant branding tested
- Realtime update behavior tested if applicable
- E2E path covered

# Definition of Done — Full Module

- Specification implemented or remaining gaps explicitly listed
- All commands/queries covered
- Lifecycle/state machine tested
- Audit/analytics integrated
- Imports supported where specified
- Seed data exists
- Public/admin projections exist where specified
- Observability exists
- Acceptance walkthrough passes
