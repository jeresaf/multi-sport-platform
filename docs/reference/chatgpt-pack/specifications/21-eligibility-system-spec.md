# Eligibility System — Full Specification

## 1. Purpose

Evaluate whether a person, participant, team, membership, roster member, or registration satisfies the Ruleset requirements for a Competition Edition or Contest.

---

# 2. Scope

In scope:
- Rule evaluation
- Eligibility checks
- Check results
- Reasons
- Overrides
- Expiry
- Re-evaluation
- Stage/contest-specific checks
- Suspension/document/deadline references

Out of scope:
- Defining person biodata
- Defining ruleset values
- Registration decision
- Discipline ownership
- Payment processing

---

# 3. Dependencies

- Ruleset Registry
- Person Registry
- Participant Registry
- Team Membership
- Competition Edition
- Registration
- Roster
- Discipline source where present
- Audit

---

# 4. Core Model

```text
EligibilityEvaluation
EligibilityCheckResult
EligibilityOverride
EligibilityRequirementSnapshot
```

Statuses:

```text
ELIGIBLE
INELIGIBLE
CONDITIONAL
PENDING
EXPIRED
OVERRIDDEN
```

---

# 5. Check Types

Examples:

```text
AGE
GENDER_CLASSIFICATION
WEIGHT_CLASS
REGISTRATION_STATUS
ROSTER_STATUS
MEMBERSHIP_ACTIVE
TRANSFER_WINDOW
DOCUMENT_REQUIRED
SUSPENSION
DEADLINE
TEAM_LIMIT
CUSTOM_RULE
```

---

# 6. Business Rules

## RULE-ELG-001
Eligibility result must record which ruleset version was evaluated.

## RULE-ELG-002
Eligibility is contextual; a participant may be eligible in one edition and not another.

## RULE-ELG-003
Overrides require permission, reason, and audit.

## RULE-ELG-004
Eligibility should be re-evaluated when dependent data changes.

## RULE-ELG-005
Expired eligibility cannot silently remain valid.

---

# 7. Commands

```text
eligibility.evaluate
eligibility.re_evaluate
eligibility.override
eligibility.remove_override
```

---

# 8. Queries

```text
Get eligibility
Get failed checks
Get conditional checks
Get override history
Get eligible participants
```

---

# 9. Events

```text
EligibilityEvaluated
EligibilityFailed
EligibilityPassed
EligibilityChanged
EligibilityOverridden
```

---

# 10. Permissions

```text
eligibility.read
eligibility.evaluate
eligibility.override
```

---

# 11. UI

Eligibility Summary  
Failed Requirements  
Override Dialog  
Eligibility History  
Bulk Eligibility Review

---

# 12. Errors

```text
ELIGIBILITY_CONTEXT_INVALID
ELIGIBILITY_RULESET_MISSING
ELIGIBILITY_OVERRIDE_FORBIDDEN
ELIGIBILITY_EXPIRED
```

---

# 13. Tests

- Age
- Roster
- Membership
- Deadline
- Suspension
- Override
- Rule-version traceability
- Re-evaluation

---

# 14. Definition of Done

Complete when registration, roster, and contest selection can make consistent eligibility decisions against the exact edition ruleset.
