# Transfer Management System — Full Specification

## 1. Purpose

Provide an optional, ruleset-controlled workflow for moving athlete membership between teams while preserving history and competition eligibility.

Transfer Management is a capability, not a universal requirement for all sports.

---

# 2. Scope

In scope:
- Transfer request
- Source team
- Destination team
- Athlete
- Transfer type
- Effective date
- Approval workflow
- Transfer windows
- Loan
- Release
- Registration effects
- Audit/history

Out of scope:
- Payments/contract settlement
- Legal contract storage unless later added
- Person identity
- Team identity
- Roster management ownership
- Federation disciplinary decisions

---

# 3. Dependencies

- Team Membership
- Team Registry
- Participant Registry
- Ruleset Registry
- Competition Management
- Roster Management
- Audit
- Entitlements

---

# 4. Transfer Types

```text
PERMANENT
LOAN
RETURN_FROM_LOAN
RELEASE
INTERNAL
SPECIAL_EXCEPTION
```

---

# 5. Core Model

```text
TransferRequest
TransferApproval
TransferWindow
TransferEffect
TransferHistory
```

Fields:

```text
id
tenant_id
participant_id
source_team_id
destination_team_id
transfer_type
requested_effective_date
approved_effective_date
status
reason
requested_by
submitted_at
approved_at
completed_at
version
```

---

# 6. Lifecycle

```text
DRAFT
SUBMITTED
UNDER_REVIEW
APPROVED
REJECTED
CANCELLED
COMPLETED
```

---

# 7. Business Rules

## RULE-TRN-001
Transfer functionality must be enabled by entitlement and applicable ruleset/policy.

## RULE-TRN-002
Source and destination team must differ unless transfer type explicitly allows otherwise.

## RULE-TRN-003
Effective date must comply with transfer window unless authorized exception applies.

## RULE-TRN-004
Completing transfer closes/updates source membership and creates/activates destination membership transactionally.

## RULE-TRN-005
Historical roster/results remain unchanged.

## RULE-TRN-006
Competition roster effects must be explicit; transfer completion does not silently rewrite historical rosters.

## RULE-TRN-007
Loan creates time-bounded membership behavior and preserves parent/source relationship.

## RULE-TRN-008
Rejected/cancelled transfers never alter membership.

---

# 8. Commands

```text
transfer.create
transfer.submit
transfer.review
transfer.approve
transfer.reject
transfer.cancel
transfer.complete
transfer.exception.approve
```

---

# 9. Queries

```text
List transfers
Get transfer
Get participant transfer history
Get team incoming transfers
Get team outgoing transfers
Check transfer window
Get affected rosters
```

---

# 10. Events

```text
TransferRequested
TransferSubmitted
TransferApproved
TransferRejected
TransferCancelled
TransferCompleted
ParticipantTeamChanged
```

---

# 11. Permissions

```text
transfer.read
transfer.create
transfer.submit
transfer.review
transfer.approve
transfer.reject
transfer.exception.approve
```

---

# 12. Entitlements

```text
TRANSFER_MANAGEMENT
LOAN_MANAGEMENT
TRANSFER_EXCEPTIONS
```

---

# 13. Audit

Audit all decisions, exceptions, effective-date changes, approvals, and completion.

Reason mandatory for rejection, exception, and manual completion override.

---

# 14. Integrity

- Tenant scoped
- Valid source/destination
- Participant exists
- Membership exists when required
- Window validation
- Idempotent completion
- Transactional membership update
- No historical destructive update

---

# 15. UI

Transfer Inbox  
Create Transfer  
Transfer Detail  
Review/Approval  
Transfer Windows  
Participant Transfer History

---

# 16. Notifications

Possible:
- transfer submitted
- transfer approved
- transfer rejected
- transfer completed
- roster action required

---

# 17. Errors

```text
TRANSFER_NOT_FOUND
TRANSFER_NOT_ENABLED
TRANSFER_WINDOW_CLOSED
TRANSFER_SOURCE_INVALID
TRANSFER_DESTINATION_INVALID
TRANSFER_MEMBERSHIP_INVALID
TRANSFER_ALREADY_COMPLETED
TRANSFER_EXCEPTION_REQUIRED
```

---

# 18. Tests

- Permanent transfer
- Loan
- Return from loan
- Closed window
- Exception
- Rejection
- Cancellation
- Transactional completion
- Roster impact
- Historical preservation

---

# 19. Definition of Done

Complete when enabled sports/competitions can manage athlete movement between teams without corrupting memberships, rosters, or historical competition records.
