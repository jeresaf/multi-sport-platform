# Match Squad / Lineup System — Full Specification

## 1. Purpose

Own contest-specific participant selections, starters, bench/replacements, positions, and lineup submission.

---

# 2. Scope

In scope:
- Match squad
- Starter/bench/replacement selection
- Positions
- Captain
- Goalkeeper/special roles
- Submission/approval
- Lineup lock
- Late change
- Eligibility validation

Out of scope:
- Competition roster ownership
- Team membership
- Scoring
- Timeline
- Permanent player position

---

# 3. Dependencies

- Roster Management
- Eligibility
- Contest Operations
- Ruleset
- Sports Definition
- Audit

---

# 4. Core Model

```text
ContestSquad
ContestSquadMember
StartingLineup
LineupPosition
LineupStatusHistory
```

Status:

```text
DRAFT
SUBMITTED
APPROVED
LOCKED
LATE_CHANGE
CLOSED
```

---

# 5. Business Rules

## RULE-LIN-001
Selection must come from eligible competition roster unless ruleset allows exception.

## RULE-LIN-002
Squad size and starter count come from ruleset.

## RULE-LIN-003
Required specialist positions/roles must validate.

## RULE-LIN-004
Locked lineup changes require authorized late-change workflow.

## RULE-LIN-005
Lineup history must be preserved.

---

# 6. Commands

```text
lineup.create
lineup.member.add
lineup.member.remove
lineup.starter.set
lineup.position.set
lineup.captain.set
lineup.submit
lineup.approve
lineup.lock
lineup.late_change
```

---

# 7. Queries

```text
Get contest lineup
Get eligible roster members
Get starters
Get bench
Get lineup validation
Get lineup history
```

---

# 8. Events

```text
LineupCreated
LineupSubmitted
LineupApproved
LineupLocked
LineupLateChanged
```

---

# 9. UI

Lineup Builder  
Formation/Position View  
Bench/Replacements  
Validation  
Submit/Approve  
Late Change

---

# 10. Errors

```text
LINEUP_MEMBER_NOT_ELIGIBLE
LINEUP_LIMIT_EXCEEDED
LINEUP_STARTER_COUNT_INVALID
LINEUP_REQUIRED_POSITION_MISSING
LINEUP_LOCKED
```

---

# 11. Definition of Done

Complete when each contest can validate and publish sport-specific squads/lineups drawn from the approved competition roster.
