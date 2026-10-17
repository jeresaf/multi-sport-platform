# Progression System — Full Specification

## 1. Purpose

Convert approved results into competition advancement, standings, bracket movement, series state, qualification, and completion.

---

# 2. Scope

In scope:
- Standings calculation
- Group ranking
- Qualification
- Bracket advancement
- Series wins
- Heat qualification
- Leaderboard progression
- Placement progression
- Competition-stage completion
- Recalculation after corrected approved result

Out of scope:
- Live scoring
- Result approval
- Competition format template ownership
- Sport statistic aggregation beyond progression needs

---

# 3. Dependencies

- Competition Edition
- Stage Instantiation
- Competition Format
- Ruleset
- Results & Approval
- Contest Scheduling
- Participant Registry
- Audit

---

# 4. Core Model

```text
StandingTable
StandingRow
QualificationDecision
BracketAdvancement
SeriesState
HeatQualification
LeaderboardState
ProgressionRun
ProgressionOverride
```

Derived state should be reproducible.

---

# 5. Business Rules

## RULE-PRG-001
Only approved results feed official progression.

## RULE-PRG-002
Progression uses exact bound format/ruleset versions.

## RULE-PRG-003
Recalculation after result correction must be deterministic.

## RULE-PRG-004
Manual override requires permission, reason, and audit.

## RULE-PRG-005
Tie-break sequence is ruleset-driven.

## RULE-PRG-006
Bracket advancement must preserve source-result traceability.

## RULE-PRG-007
Derived state must not be hand-edited directly except through explicit override workflow.

---

# 6. Commands

```text
progression.recalculate
progression.override
progression.confirm_stage
progression.advance
progression.rollback_recalculate
```

---

# 7. Queries

```text
Get standings
Get group table
Get bracket
Get series state
Get leaderboard
Get qualification status
Get progression history
```

---

# 8. Events

```text
StandingsRecalculated
ParticipantQualified
BracketAdvanced
SeriesUpdated
StageCompleted
ProgressionOverridden
CompetitionCompleted
```

---

# 9. UI

Standings  
Groups  
Bracket  
Series  
Leaderboard  
Qualification Summary  
Progression Diagnostics

---

# 10. Errors

```text
PROGRESSION_RESULT_NOT_APPROVED
PROGRESSION_RULESET_INVALID
PROGRESSION_TIE_UNRESOLVED
PROGRESSION_OVERRIDE_REQUIRED
PROGRESSION_RECALCULATION_FAILED
```

---

# 11. Tests

- League standings
- Bonus points
- Group tiebreak
- Group+knockout qualification
- Best-of series
- Rugby 7s placement bracket
- Athletics heat qualification
- Golf leaderboard
- Corrected result recalculation

---

# 12. Definition of Done

Complete when approved results deterministically drive every supported competition format without manual table/bracket editing under normal operation.
