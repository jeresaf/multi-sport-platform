# Competition Edition / Season System — Full Specification

## 1. Purpose

Represent a specific operational occurrence of a Competition and bind the immutable configuration used to run it.

Example:

```text
Uganda Premier League
  └── 2026/27

Kampala Sevens
  └── 2026 Edition
```

---

# 2. Scope

In scope:
- Edition identity
- Dates
- Season/edition label
- Sport definition version
- Competition format version
- Ruleset version
- Registration windows
- Publication state
- Enabled features
- Operational lifecycle
- Competition-level configuration snapshot
- Edition participants reference
- Edition closure/archive

Out of scope:
- Actual registration decision details
- Fixtures
- Results
- Timeline
- Player identity

---

# 3. Dependencies

- Competition Management
- Sports Definition
- Competition Format
- Ruleset Registry
- Tenant Management
- Audit

Downstream:
- Registration
- Eligibility
- Stage Instantiation
- Scheduling
- Contest Operations
- Progression
- Reporting

---

# 4. Core Entity

```text
CompetitionEdition
```

Suggested fields:

```text
id
tenant_id
competition_id
edition_code
display_name
season_label
start_date
end_date
registration_open_at
registration_close_at
sport_definition_version_id
competition_format_version_id
ruleset_version_id
status
public_enabled
published_at
closed_at
archived_at
config_snapshot_hash
version
```

---

# 5. Lifecycle

```text
DRAFT
REGISTRATION_OPEN
REGISTRATION_CLOSED
SCHEDULED
ACTIVE
COMPLETED
UNDER_REVIEW
CLOSED
ARCHIVED
CANCELLED
```

---

# 6. Business Rules

## RULE-EDN-001
Edition binds exact versions of Sport Definition, Competition Format, and Ruleset.

## RULE-EDN-002
Bound published configuration cannot be silently replaced after Edition becomes ACTIVE.

## RULE-EDN-003
Date range must be valid.

## RULE-EDN-004
Registration dates must fall within allowed operational policy.

## RULE-EDN-005
CLOSED edition may only be reopened through privileged audited workflow.

## RULE-EDN-006
ARCHIVED edition remains historically readable.

## RULE-EDN-007
Edition cannot become ACTIVE until required structure/participants/readiness checks pass.

---

# 7. Commands

```text
edition.create
edition.update
edition.open_registration
edition.close_registration
edition.schedule
edition.activate
edition.complete
edition.review
edition.close
edition.reopen
edition.cancel
edition.archive
edition.publish
```

---

# 8. Queries

```text
Get edition
List editions
Get effective configuration
Get readiness summary
Get participant summary
Get schedule summary
Get progression summary
```

---

# 9. Events

```text
CompetitionEditionCreated
RegistrationOpened
RegistrationClosed
CompetitionEditionActivated
CompetitionEditionCompleted
CompetitionEditionClosed
CompetitionEditionReopened
CompetitionEditionArchived
```

---

# 10. Permissions

```text
edition.read
edition.create
edition.update
edition.publish
edition.lifecycle.manage
edition.reopen
```

---

# 11. Audit

All lifecycle/configuration binding changes audited.

---

# 12. UI

Edition dashboard:

```text
Status
Dates
Configuration
Registration
Participants
Stages
Schedule
Results
Progression
Issues
Next Actions
```

---

# 13. Errors

```text
EDITION_NOT_FOUND
EDITION_CONFIG_INVALID
EDITION_INVALID_DATE_RANGE
EDITION_INVALID_TRANSITION
EDITION_CONFIG_IMMUTABLE
EDITION_NOT_READY
```

---

# 14. Tests

- Version binding
- Activation readiness
- Registration lifecycle
- Close/reopen
- Historical config immutability
- Tenant isolation

---

# 15. Definition of Done

Complete when each season/edition can be reproduced historically from immutable configuration bindings and lifecycle history.
