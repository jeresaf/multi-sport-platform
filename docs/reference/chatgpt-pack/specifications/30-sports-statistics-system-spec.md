# Sports Statistics System — Full Specification

## 1. Purpose

Produce trusted sport-performance statistics from approved results, structured timelines, lineups, and official stat inputs without becoming the source of truth for contests or participants.

---

# 2. Scope

In scope:
- Player statistics
- Team statistics
- Contest statistics
- Competition/season aggregates
- Career aggregates
- Derived metrics
- Stat corrections/recalculation
- Coverage/confidence metadata
- Ranking/stat leaderboards

Out of scope:
- Live contest state
- Result approval
- Timeline event ownership
- Competition progression
- Platform/product analytics

---

# 3. Dependencies

- Sports Definition
- Timeline
- Results & Approval
- Match Squad / Lineup
- Participant Registry
- Team Registry
- Competition Edition
- Audit

---

# 4. Core Model

```text
StatisticDefinitionRef
ContestStatistic
ParticipantStatistic
TeamStatistic
CompetitionAggregate
CareerAggregate
StatisticRecalculationRun
StatisticCorrection
```

Every stat should trace to:
- sport definition version
- source coverage
- source events/results
- aggregation period

---

# 5. Business Rules

## RULE-STAT-001
Official aggregates should normally use approved results and confirmed events.

## RULE-STAT-002
Provisional live stats may exist but must be marked provisional.

## RULE-STAT-003
Derived statistics must be reproducible.

## RULE-STAT-004
Correction of a source event/result triggers recalculation.

## RULE-STAT-005
Coverage level must be exposed so UI does not imply unavailable precision.

## RULE-STAT-006
Stat definitions come from Sports Definition, not arbitrary ad hoc fields.

---

# 6. Commands

```text
statistics.record_manual
statistics.recalculate_contest
statistics.recalculate_competition
statistics.correct
statistics.rebuild_career
```

---

# 7. Queries

```text
Get contest stats
Get player stats
Get team stats
Get competition leaders
Get season totals
Get career totals
Get stat coverage
```

---

# 8. Events

```text
ContestStatisticsUpdated
ParticipantStatisticsUpdated
TeamStatisticsUpdated
CompetitionStatisticsRecalculated
StatisticCorrectionApplied
```

---

# 9. UI

Statistics tabs should be sport-specific but consume canonical stat definitions.

Views:
- Match stats
- Player box score
- Team comparison
- Competition leaders
- Season totals
- Career records

---

# 10. Errors

```text
STAT_DEFINITION_UNKNOWN
STAT_SOURCE_INCOMPLETE
STAT_RECALCULATION_FAILED
STAT_CORRECTION_INVALID
```

---

# 11. Tests

- Football goals/assists
- Basketball points/rebounds/assists
- Rugby tries/conversions
- Cricket runs/wickets
- Recalculation after correction
- Provisional vs official
- Coverage metadata

---

# 12. Definition of Done

Complete when sport-specific statistics can be recalculated reliably from authoritative sporting data and exposed consistently to public/admin UIs.
