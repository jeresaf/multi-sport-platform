# Roster Management System — Full Specification

## 1. Purpose

Own competition-specific team/player eligibility lists while keeping them separate from permanent team membership and contest-specific match squads.

### Hierarchy

```text
Team Membership
   ↓
Competition Roster
   ↓
Roster Member
   ↓
Match Squad
   ↓
Starting Lineup / Bench
```

---

# 2. Scope

In scope:
- Competition roster
- Roster membership
- Competition-specific shirt number
- Position
- Roster eligibility state
- Approval workflow
- Roster limits
- Registration deadlines
- Roster locking
- Replacement rules where ruleset allows

Out of scope:
- Permanent team membership
- Match squad ownership
- Transfer workflow
- Competition registration payment
- Person identity

---

# 3. Dependencies

- Team Membership
- Competition Edition
- Ruleset Registry
- Participant Registry
- Team Registry
- Eligibility service
- Audit

Downstream:
- Contest Operations
- Lineups
- Statistics
- Public squad views

---

# 4. Core Model

```text
CompetitionRoster
RosterMember
RosterStatusHistory
RosterLock
```

CompetitionRoster fields:

```text
id
tenant_id
competition_edition_id
team_id
status
submitted_at
approved_at
locked_at
version
```

RosterMember fields:

```text
id
roster_id
participant_id
membership_id
shirt_number
position_code
status
eligibility_status
registered_at
removed_at
reason
```

---

# 5. Roster Status

```text
DRAFT
SUBMITTED
UNDER_REVIEW
APPROVED
LOCKED
REOPENED
CLOSED
```

Roster-member status:

```text
PENDING
ELIGIBLE
INELIGIBLE
SUSPENDED
REMOVED
TRANSFERRED_OUT
```

---

# 6. Business Rules

## RULE-ROS-001
Roster limits come from bound ruleset.

## RULE-ROS-002
Roster member normally requires valid team membership unless ruleset explicitly permits guest/composite participants.

## RULE-ROS-003
A locked roster cannot be edited without authorized reopen workflow.

## RULE-ROS-004
Competition-specific shirt number may differ from team-membership default.

## RULE-ROS-005
Roster eligibility is competition-specific and does not rewrite participant status.

## RULE-ROS-006
Removed roster members remain historically recorded.

## RULE-ROS-007
Match squad may only select eligible roster members unless an authorized exception exists.

---

# 7. Commands

```text
roster.create
roster.member.add
roster.member.remove
roster.submit
roster.review
roster.approve
roster.lock
roster.reopen
roster.close
roster.member.eligibility_override
```

---

# 8. Queries

```text
Get competition roster
List roster members
Get eligibility summary
Get available team members not rostered
Get roster history
Get match-squad eligible members
```

---

# 9. Events

```text
RosterCreated
RosterMemberAdded
RosterSubmitted
RosterApproved
RosterLocked
RosterReopened
RosterMemberEligibilityChanged
```

---

# 10. Permissions

```text
roster.read
roster.create
roster.manage
roster.submit
roster.review
roster.approve
roster.reopen
roster.eligibility.override
```

---

# 11. Entitlements

Possible:

```text
ROSTER_MANAGEMENT
ADVANCED_ELIGIBILITY
ROSTER_IMPORT
```

---

# 12. Audit

Audit all adds/removes, eligibility overrides, approval, lock/reopen, and deadline exceptions.

---

# 13. Integrity

- Unique team+competition roster
- Unique active participant per roster
- Ruleset limits enforced
- Membership relationship validated
- Locked-state enforcement
- Historical records preserved

---

# 14. Import / Export

Support Excel/CSV roster templates with:

```text
Download Template
Upload
Map
Validate
Resolve Person/Participant
Check Membership
Check Eligibility
Preview
Import
```

---

# 15. UI

Roster page:

```text
Team
Competition
Status
Roster Count / Limit
Eligibility Summary
Submission Deadline
```

Actions:
- Add member
- Bulk import
- Submit
- Approve
- Lock
- Reopen

---

# 16. Errors

```text
ROSTER_NOT_FOUND
ROSTER_LIMIT_EXCEEDED
ROSTER_LOCKED
ROSTER_MEMBER_DUPLICATE
ROSTER_MEMBER_NOT_ELIGIBLE
ROSTER_MEMBERSHIP_REQUIRED
ROSTER_DEADLINE_PASSED
```

---

# 17. Tests

- Ruleset min/max
- Add/remove
- Submission
- Approval
- Lock
- Reopen
- Eligibility
- Shirt numbers
- Import
- Match-squad candidate list

---

# 18. Definition of Done

Complete when every team competition entry can maintain an approved, historically accurate, ruleset-compliant roster feeding contest squads and lineups.
