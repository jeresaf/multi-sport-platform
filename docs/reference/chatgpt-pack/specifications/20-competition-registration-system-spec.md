# Competition Registration System — Full Specification

## 1. Purpose

Own the workflow through which teams/participants apply for and are accepted into a Competition Edition.

---

# 2. Scope

In scope:
- Registration application
- Applicant participant/team
- Submission
- Required documents references
- Registration fees/status reference
- Review notes
- Approval/rejection
- Withdrawal
- Waitlist
- Disqualification from entry
- Registration history

Out of scope:
- Person identity
- Team membership
- Competition roster details
- Eligibility engine internals
- Billing/payment implementation

---

# 3. Dependencies

- Competition Edition
- Participant Registry
- Team Registry
- Eligibility System
- Ruleset Registry
- Audit
- Entitlements

---

# 4. Core Model

```text
CompetitionRegistration
RegistrationDocument
RegistrationDecision
RegistrationHistory
```

Fields:

```text
id
tenant_id
competition_edition_id
participant_id
team_id
registration_type
status
submitted_at
reviewed_at
approved_at
withdrawn_at
decision_reason
created_by
version
```

---

# 5. Lifecycle

```text
DRAFT
SUBMITTED
UNDER_REVIEW
APPROVED
REJECTED
WAITLISTED
WITHDRAWN
DISQUALIFIED
```

---

# 6. Business Rules

## RULE-REG-001
One active registration per applicant per edition unless multi-entry explicitly supported.

## RULE-REG-002
Submission after deadline requires authorized exception.

## RULE-REG-003
Approval requires eligibility result that is acceptable under ruleset.

## RULE-REG-004
Approved registration does not automatically create roster members.

## RULE-REG-005
Withdrawal/disqualification preserves registration history.

## RULE-REG-006
Registration applicant type must be compatible with edition configuration.

---

# 7. Commands

```text
registration.create
registration.update
registration.submit
registration.review
registration.approve
registration.reject
registration.waitlist
registration.withdraw
registration.disqualify
registration.exception.approve
```

---

# 8. Queries

```text
List registrations
Get registration
Get pending reviews
Get applicant status
Get registration summary
```

---

# 9. Events

```text
RegistrationCreated
RegistrationSubmitted
RegistrationApproved
RegistrationRejected
RegistrationWaitlisted
RegistrationWithdrawn
RegistrationDisqualified
```

---

# 10. Permissions

```text
registration.read
registration.create
registration.submit
registration.review
registration.approve
registration.reject
registration.exception
```

---

# 11. UI

Registration Inbox  
Registration Detail  
Applicant Review  
Eligibility Summary  
Documents  
Decision History  
Bulk Review

---

# 12. Import

Bulk registration import must resolve team/participant and run eligibility checks.

---

# 13. Errors

```text
REGISTRATION_NOT_FOUND
REGISTRATION_DUPLICATE
REGISTRATION_DEADLINE_PASSED
REGISTRATION_NOT_ELIGIBLE
REGISTRATION_INVALID_APPLICANT_TYPE
REGISTRATION_ALREADY_DECIDED
```

---

# 14. Definition of Done

Complete when competition entry decisions are explicit, auditable, and distinct from permanent participant/team identity and roster composition.
