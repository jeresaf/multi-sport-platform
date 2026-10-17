# Live Scoring System — Full Specification

## 1. Purpose

Own provisional live score state and score-affecting actions during a contest using the sport definition and ruleset.

---

# 2. Scope

In scope:
- Score state
- Score-affecting commands
- Score validation
- Score snapshots
- Idempotency
- Manual correction workflow
- Offline scorer sync integration
- Real-time score publication

Out of scope:
- Timeline full event catalogue
- Result approval
- Standings
- Schedule
- Sport scoring-action definitions

---

# 3. Dependencies

- Contest Operations
- Sports Definition
- Ruleset
- Timeline
- Match Squad/Lineup
- Audit
- SignalR/realtime infrastructure

---

# 4. Core Model

```text
ContestScoreState
ScoreComponent
ScoreChange
ScoreSnapshot
ScoreCorrection
```

Must support:
- simple integer scores
- period scores
- cricket complex scores
- tennis/volleyball sets
- penalties/shootout secondary scores
- other configured sport score structures

---

# 5. Business Rules

## RULE-SCR-001
Score-affecting actions must be valid for current sport/phase.

## RULE-SCR-002
Score command must include idempotency key.

## RULE-SCR-003
Score after action must reconcile with timeline event where applicable.

## RULE-SCR-004
Corrections preserve original event/history.

## RULE-SCR-005
Approved final result cannot be silently overwritten by live scoring.

## RULE-SCR-006
Offline sync conflicts require review, not last-write-wins.

---

# 6. Commands

```text
score.apply_action
score.correct
score.reverse_action
score.reconcile
score.offline_sync
```

Examples per sport are resolved from definitions:

```text
GOAL
TRY
CONVERSION
FREE_THROW
THREE_POINT
WICKET/RUN event combination
```

---

# 7. Queries

```text
Get current score
Get score components
Get period scores
Get score history
Get reconciliation status
```

---

# 8. Events

```text
ContestScoreChanged
ContestScoreCorrected
ContestScoreReconciled
ScoreConflictDetected
```

---

# 9. UI

Quick scoring controls by sport  
Score preview  
Undo/correct last action  
Conflict indicator  
Offline queue indicator

---

# 10. Errors

```text
SCORE_ACTION_INVALID
SCORE_DUPLICATE_ACTION
SCORE_STATE_CONFLICT
SCORE_CORRECTION_REQUIRED
SCORE_OFFLINE_CONFLICT
```

---

# 11. Definition of Done

Complete when flagship sports can update live score safely, idempotently, and consistently with timeline/history.
