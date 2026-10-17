# Results & Approval System — Full Specification

## 1. Purpose

Own the official result workflow after sporting action ends, including submission, review, approval, dispute, correction, and closure.

---

# 2. Scope

In scope:
- Draft result
- Submitted result
- Review
- Official result
- Approval/rejection
- Dispute
- Correction
- Result versions
- Reopen
- Result audit
- Contest close integration

Out of scope:
- Live score ownership
- Timeline event ownership
- Standings/progression ownership
- Discipline decisions

---

# 3. Dependencies

- Contest Operations
- Live Scoring
- Timeline
- Ruleset
- Officials
- Audit

Downstream:
- Progression
- Statistics
- Public Read Models
- Historical Records

---

# 4. Result Status

```text
NONE
DRAFT
SUBMITTED
UNDER_REVIEW
APPROVED
CORRECTED
DISPUTED
REJECTED
```

---

# 5. Core Model

```text
ContestResult
ResultVersion
ResultSubmission
ResultReview
ResultCorrection
ResultDispute
```

---

# 6. Business Rules

## RULE-RES-001
Contest may be ENDED while result is still pending.

## RULE-RES-002
Contest becomes CLOSED only after approved result unless sport/format policy explicitly differs.

## RULE-RES-003
Approved result correction creates new version and preserves old version.

## RULE-RES-004
Result should reconcile with live score/timeline before approval or show explicit warning.

## RULE-RES-005
Correction/reopen requires permission and reason.

## RULE-RES-006
Progression consumes approved result versions only.

---

# 7. Commands

```text
result.create_draft
result.submit
result.review
result.approve
result.reject
result.dispute
result.correct
result.reopen
```

---

# 8. Queries

```text
Get current result
Get result versions
Get pending approvals
Get disputes
Get reconciliation summary
```

---

# 9. Events

```text
ResultSubmitted
ResultApproved
ResultRejected
ResultDisputed
ResultCorrected
ResultReopened
```

---

# 10. Permissions

```text
result.read
result.submit
result.review
result.approve
result.reject
result.dispute
result.correct
result.reopen
```

---

# 11. UI

Pending Results Inbox  
Result Review  
Reconciliation Panel  
Approve/Reject  
Correction Workflow  
Version History

---

# 12. Errors

```text
RESULT_NOT_FOUND
RESULT_NOT_RECONCILED
RESULT_ALREADY_APPROVED
RESULT_CORRECTION_REASON_REQUIRED
RESULT_PERMISSION_REQUIRED
```

---

# 13. Tests

- Submit
- Approve
- Reject
- Dispute
- Correct approved result
- Reopen
- Close contest
- Progression waits for approval

---

# 14. Definition of Done

Complete when every official result has an auditable approval/version history and downstream progression only consumes approved truth.
