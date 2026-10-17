# State Machine Catalogue

This catalogue centralizes lifecycle states so modules do not invent overlapping status semantics.

## Tenant

```text
PROVISIONING
→ ACTIVE
→ EXPIRING
→ GRACE
→ READ_ONLY
→ SUSPENDED
→ ARCHIVED
```

Recovery:
```text
EXPIRING / GRACE / READ_ONLY / SUSPENDED → ACTIVE
```

## Subscription

```text
DRAFT
→ ACTIVE
→ EXPIRING
→ GRACE
→ EXPIRED
```

Additional:
```text
ACTIVE → SUSPENDED
ACTIVE → CANCELLED
```

## Sport / Format / Ruleset Version

```text
DRAFT
→ PUBLISHED
→ DEPRECATED
→ ARCHIVED
```

Published versions are immutable.

## Person

```text
ACTIVE
INACTIVE
DECEASED
MERGED
ARCHIVED
```

## Participant / Staff / Official

```text
ACTIVE
INACTIVE
SUSPENDED
RETIRED
ARCHIVED
```

## Organisation

```text
ACTIVE
INACTIVE
SUSPENDED
DISSOLVED
ARCHIVED
```

## Team

```text
ACTIVE
INACTIVE
SUSPENDED
DISBANDED
ARCHIVED
```

## Venue

```text
ACTIVE
TEMPORARILY_UNAVAILABLE
INACTIVE
CLOSED
ARCHIVED
```

## Team Membership

```text
PENDING
→ ACTIVE
→ INACTIVE / SUSPENDED / TRANSFERRED_OUT / RELEASED / EXPIRED
```

## Transfer

```text
DRAFT
→ SUBMITTED
→ UNDER_REVIEW
→ APPROVED
→ COMPLETED
```

Alternate terminal:
```text
REJECTED
CANCELLED
```

## Competition

```text
DRAFT
→ ACTIVE
→ INACTIVE / SUSPENDED
→ ARCHIVED
```

## Competition Edition

```text
DRAFT
→ REGISTRATION_OPEN
→ REGISTRATION_CLOSED
→ SCHEDULED
→ ACTIVE
→ COMPLETED
→ UNDER_REVIEW
→ CLOSED
→ ARCHIVED
```

Alternate:
```text
CANCELLED
```

## Registration

```text
DRAFT
→ SUBMITTED
→ UNDER_REVIEW
→ APPROVED / REJECTED / WAITLISTED
```

Post decision:
```text
WITHDRAWN
DISQUALIFIED
```

## Eligibility

```text
PENDING
→ ELIGIBLE / INELIGIBLE / CONDITIONAL
→ EXPIRED
```

Override:
```text
OVERRIDDEN
```

## Competition Roster

```text
DRAFT
→ SUBMITTED
→ UNDER_REVIEW
→ APPROVED
→ LOCKED
→ CLOSED
```

Exception:
```text
LOCKED → REOPENED → APPROVED/LOCKED
```

## Roster Member

```text
PENDING
→ ELIGIBLE / INELIGIBLE / SUSPENDED / REMOVED / TRANSFERRED_OUT
```

## Lineup

```text
DRAFT
→ SUBMITTED
→ APPROVED
→ LOCKED
→ CLOSED
```

Exception:
```text
LOCKED → LATE_CHANGE
```

## Schedule

```text
DRAFT
→ PUBLISHED
→ RESCHEDULED / POSTPONED / CANCELLED
```

This is distinct from contest lifecycle.

## Contest

```text
NOT_STARTED
→ STARTING_SOON
→ LIVE
→ ENDED
→ CLOSED
```

Exceptional:
```text
NOT_STARTED → DELAYED / POSTPONED / CANCELLED
LIVE → INTERRUPTED / SUSPENDED / ABANDONED
INTERRUPTED → LIVE / SUSPENDED / ABANDONED
SUSPENDED → LIVE / POSTPONED / ABANDONED
```

## Result

```text
NONE
→ DRAFT
→ SUBMITTED
→ UNDER_REVIEW
→ APPROVED
```

Alternate:
```text
REJECTED
DISPUTED
```

After approval:
```text
APPROVED → CORRECTED
```

## Timeline Event

```text
ACTIVE
PENDING_REVIEW
CONFIRMED
CORRECTED
REVERSED
VOID
```

## Import Job

```text
CREATED
→ MAPPED
→ VALIDATED
→ DRY_RUN_COMPLETE
→ RUNNING
→ COMPLETED
```

Alternate:
```text
PARTIAL_FAILURE
FAILED
CANCELLED
```

## Report Run

```text
QUEUED
→ RUNNING
→ COMPLETED
```

Alternate:
```text
FAILED
EXPIRED
```
