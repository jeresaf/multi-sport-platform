# Stage / Group Instantiation System — Full Specification

## 1. Purpose

Instantiate the selected Competition Format into real edition-specific stages, groups, rounds, slots, and progression relationships.

---

# 2. Scope

In scope:
- Stage instances
- Group/pool instances
- Round instances
- Bracket slots
- Participant placement
- Seeding
- Draw
- Progression links
- Manual/automatic structure generation

Out of scope:
- Format template ownership
- Match scheduling times
- Results
- Live contest state

---

# 3. Dependencies

- Competition Edition
- Competition Format System
- Ruleset Registry
- Registration
- Participant Registry
- Audit

---

# 4. Core Model

```text
CompetitionStage
CompetitionGroup
CompetitionRound
CompetitionSlot
StageParticipant
ProgressionLink
DrawResult
SeedingRecord
```

---

# 5. Stage Types

```text
LEAGUE
GROUP
POOL
KNOCKOUT
SERIES
HEAT
FINAL
LEADERBOARD
PLACEMENT
FIGHT_CARD
```

---

# 6. Business Rules

## RULE-STG-001
Instantiated structure must conform to bound format version.

## RULE-STG-002
Participant count must satisfy format/ruleset constraints or explicitly enter partial/manual state.

## RULE-STG-003
Draw/seeding must be reproducible from stored inputs and random seed if randomized.

## RULE-STG-004
Published structure cannot be silently changed after contests are generated.

## RULE-STG-005
Manual overrides require audit.

---

# 7. Commands

```text
stage.instantiate
stage.group.create
stage.participant.seed
stage.draw.run
stage.draw.override
stage.structure.publish
stage.structure.reopen
```

---

# 8. Queries

```text
Get stage structure
Get groups
Get rounds
Get bracket
Get seeding
Get progression map
```

---

# 9. Events

```text
StageStructureInstantiated
CompetitionDrawCompleted
StageParticipantsAssigned
StageStructurePublished
```

---

# 10. UI

Structure Builder  
Group/Pool Assignment  
Seeding  
Draw  
Bracket Preview  
Progression Graph

---

# 11. Errors

```text
STAGE_FORMAT_MISMATCH
STAGE_PARTICIPANT_COUNT_INVALID
STAGE_ALREADY_PUBLISHED
STAGE_DRAW_INVALID
```

---

# 12. Tests

- League
- Group+knockout
- Rugby 7s pool+placement
- Best-of series
- Heat-to-final
- Leaderboard
- Fight card

---

# 13. Definition of Done

Complete when a format template can be deterministically instantiated into real edition structure ready for fixture generation.
