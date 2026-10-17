# Historical Records System — Full Specification

## 1. Purpose

Preserve and expose durable historical sporting records across editions, teams, participants, venues, competitions, and careers.

---

# 2. Scope

In scope:
- Past editions
- Champions/winners
- Historical standings
- Historical results
- Participant history
- Team history
- Career summaries
- Records
- Milestones
- Historical aliases
- Archived public views

Out of scope:
- Primary operational writes
- Result approval
- Player biodata ownership
- Raw import parsing

---

# 3. Dependencies

- Competition Management
- Results & Approval
- Progression
- Statistics
- Team Membership
- Person/Participant Registry
- Read Models
- Audit

---

# 4. Core Model

Prefer projections and immutable record snapshots where appropriate:

```text
CompetitionHistorySnapshot
EditionSummary
ChampionRecord
TeamHistoryProjection
ParticipantHistoryProjection
CareerRecord
MilestoneRecord
HistoricalRecordVersion
```

---

# 5. Business Rules

## RULE-HIS-001
Historical records must trace to authoritative source entities/versions.

## RULE-HIS-002
Corrections update historical projections while preserving prior audit/version history.

## RULE-HIS-003
Archived source entities remain historically resolvable.

## RULE-HIS-004
Historical public pages must not expose private fields.

## RULE-HIS-005
Historical names may display period-correct aliases where configured.

---

# 6. Commands

Mostly rebuild/admin operations:

```text
history.rebuild
history.milestone.create
history.record.correct
```

---

# 7. Queries

```text
Get past editions
Get champions
Get participant history
Get team history
Get competition records
Get milestones
Get career history
```

---

# 8. Events

```text
HistoricalProjectionRebuilt
MilestoneCreated
HistoricalRecordCorrected
```

---

# 9. UI

History views:
- Competition archive
- Past champions
- Player career
- Team seasons
- Records
- Milestones

---

# 10. Tests

- Archived competition
- Renamed team
- Corrected historical result
- Player transfer history
- Multi-season career totals

---

# 11. Definition of Done

Complete when the platform can serve reliable long-term historical views without querying fragile operational state directly.
