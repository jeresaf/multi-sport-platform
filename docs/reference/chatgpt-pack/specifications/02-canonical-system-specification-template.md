# Sports Platform — Canonical System Specification Template

> Use this template for every bounded system/module in the Sports Platform. Sections should not be deleted merely because they appear empty. If a section does not apply, explicitly state `Not applicable` and explain why.

---

# 1. System Identity

## 1.1 System Name

`<Canonical system name>`

## 1.2 Short Code

`<Stable internal code>`

## 1.3 Purpose

Describe why the system exists in business terms.

## 1.4 Business Outcome

Describe the result this system enables for users or other platform systems.

---

# 2. Scope

## 2.1 In Scope

List the capabilities this system explicitly owns.

## 2.2 Out of Scope

List adjacent concerns this system must not own.

This section is mandatory because it prevents domain leakage.

## 2.3 Future Scope

List possible future extensions that should not affect the initial design.

---

# 3. Ubiquitous Language

Define every important domain term.

Recommended format:

| Term | Definition | Not To Be Confused With |
|---|---|---|
| Example | Canonical definition | Similar but different term |

Avoid synonyms in implementation once terminology is frozen.

---

# 4. Actors

Define all human/system actors.

For each actor specify:

- Role
- Tenant context
- Responsibilities
- Allowed high-level actions
- Restrictions

Examples:

```text
Platform Administrator
Tenant Administrator
Competition Administrator
Team Administrator
Scorer
Official
Public User
External Provider
System Job
```

---

# 5. Dependencies

## 5.1 Upstream Dependencies

Systems whose data/rules are required.

## 5.2 Downstream Consumers

Systems that consume this system's data/events.

## 5.3 Dependency Rules

State whether each dependency is:

```text
Synchronous command
Synchronous query
Read-model reference
Domain event
Integration event
Imported external data
```

---

# 6. Ownership

## 6.1 Source-of-Truth Entities

List entities this system owns.

## 6.2 Referenced External Entities

List IDs/entities referenced but owned elsewhere.

## 6.3 Ownership Prohibitions

Explicitly list entities/tables this module must not mutate.

---

# 7. Domain Model

For each aggregate/entity define:

## 7.1 Entity Name

Purpose:

Primary identifier:

Tenant scope:

Required fields:

Optional fields:

Relationships:

Unique constraints:

Version/concurrency field:

Soft-delete/archive behavior:

Historical behavior:

---

# 8. Value Objects

List value objects and validation.

Examples:

```text
CompetitionName
PeriodDuration
ScoreValue
DateRange
Money
PersonName
RankingRule
```

---

# 9. State Machines

Define lifecycle states.

Example:

```text
DRAFT
→ ACTIVE
→ DEPRECATED
→ ARCHIVED
```

For each transition define:

- Allowed source
- Target
- Actor/permission
- Preconditions
- Required reason
- Domain event
- Audit requirement

---

# 10. Business Rules

Give every rule a stable ID.

Example:

```text
RULE-SPT-001
An active sport-definition version cannot be modified.
```

For each rule define:

- Description
- Rationale
- Inputs
- Validation
- Error code
- Edge cases

---

# 11. Commands / Actions

Every state-changing operation should be defined.

Example:

```text
sport.create
sport.version.publish
sport.variant.add
```

For each command define:

- Action code
- Request
- Preconditions
- Permission
- Entitlement
- Domain rules
- Result
- Events emitted
- Audit entry
- Idempotency requirements

---

# 12. Queries

Define read operations required by:

- Admin UI
- Public UI
- Other modules
- Reports
- Search
- Background jobs

Queries must not be used to bypass domain ownership.

---

# 13. Domain Events

Define events emitted by this system.

For each:

```text
Event name
Version
Payload
When emitted
Consumers
Idempotency key
Replay behavior
```

---

# 14. Permissions

Define permissions independently of subscription entitlements.

Examples:

```text
sport.read
sport.create
sport.update
sport.publish
```

---

# 15. Entitlements

Define optional commercial limits/capabilities.

Examples:

```text
SPORT_CONFIGURATION
MAX_CUSTOM_SPORTS
ADVANCED_RULESETS
```

State whether a capability is:

```text
boolean
numeric quota
enum/tier
resource list
```

---

# 16. Audit Requirements

Define:

- Actions audited
- Actor
- Tenant
- Entity
- Correlation ID
- Reason requirements
- Before/after treatment
- Sensitive-field redaction

---

# 17. Data Integrity Rules

Define:

- Unique constraints
- Referential constraints
- Tenant constraints
- Delete/archive rules
- Duplicate prevention
- Historical/versioning rules
- Concurrency
- Migration rules

---

# 18. Security & Privacy

Define:

- PII
- Private fields
- Public fields
- Role-based visibility
- Encryption needs
- Export restrictions
- Retention
- Tenant isolation

---

# 19. Import / Export

Define supported formats:

```text
CSV
Excel
JSON
API
```

For imports specify:

- Template
- Mapping
- Dry run
- Validation
- Duplicate resolution
- Error report
- Idempotency
- Audit

---

# 20. API Contracts

Define:

- Admin endpoints
- Public endpoints
- Internal application contracts
- Pagination
- Filtering
- Versioning
- Error format
- Idempotency

Do not expose persistence entities directly.

---

# 21. UI Workflows

Define every major screen/flow.

For each:

- User goal
- Entry point
- Main controls
- State
- Empty state
- Loading state
- Error state
- Permission handling
- Mobile/tablet/desktop behavior
- Accessibility

---

# 22. Notifications

Define:

- Trigger
- Recipient
- Channel
- Template
- Deduplication
- Tenant branding
- Opt-in/opt-out behavior

---

# 23. Analytics

Define platform/product events emitted.

Examples:

```text
sport_definition_created
competition_format_published
ruleset_bound_to_competition
```

Do not mix product analytics with sports statistics.

---

# 24. Seed Data

Define:

- Required reference rows
- Minimum demo data
- Edge cases
- Idempotent seed keys
- Test scenarios

---

# 25. Error Catalogue

Give stable codes.

Example:

```text
SPORT_VERSION_IMMUTABLE
FORMAT_IN_USE
RULESET_INVALID
```

For each define:

- HTTP/application mapping
- User-facing message
- Diagnostic details
- Retry behavior

---

# 26. Edge Cases

Explicitly document:

- Null/missing data
- Duplicate data
- Historical records
- Archived entities
- Version conflicts
- Tenant suspension
- Provider mismatch
- Partial imports
- Unsupported configurations

---

# 27. Background Jobs

Define scheduled/event-driven work.

For each:

- Trigger
- Input
- Locking
- Retry
- Idempotency
- Dead-letter/error handling
- Audit/analytics

---

# 28. Performance Requirements

Define:

- Expected cardinality
- Query latency targets
- Pagination
- Cacheability
- Indexing considerations
- Real-time update volume
- Batch operations

---

# 29. Observability

Define:

- Logs
- Metrics
- Traces
- Alerts
- Correlation IDs
- Health checks

---

# 30. Tests

Required categories:

```text
Unit
Domain rule
State transition
Integration
Tenant isolation
Permission
Entitlement
Import
API contract
UI E2E
Migration
Performance where relevant
```

---

# 31. Acceptance Criteria

Create a numbered end-to-end checklist that proves the system fulfills its purpose.

---

# 32. Definition of Done

A system is complete only when:

- Domain boundaries are implemented
- Rules are tested
- API is documented
- Admin UI exists where required
- Public projection exists where required
- Permissions exist
- Entitlements are enforced where required
- Audit is emitted
- Seed/demo data exists
- Import/export is handled where required
- E2E tests pass
- Observability is present
- Documentation is updated

---

# 33. Implementation Sequence

List dependency-aware implementation steps.

---

# 34. Open Questions

Capture unresolved decisions without silently inventing answers.

Each open question should include:

- Decision required
- Why it matters
- Options
- Recommended default if implementation cannot wait
