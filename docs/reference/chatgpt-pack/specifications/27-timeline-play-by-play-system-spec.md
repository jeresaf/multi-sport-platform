# Timeline / Play-by-Play System — Full Specification

## 1. Purpose

Own the structured chronological event stream for contests during live play and historical replay.

---

# 2. Scope

In scope:
- Timeline events
- Event actors
- Event sequence
- Play grouping
- Importance
- Correction/reversal
- Score-after-event snapshot
- Provider normalization target
- Key vs all filtering
- Real-time event publication

Out of scope:
- Provider-specific raw schema ownership
- Final result approval
- Statistics ownership
- Sport event definition ownership

---

# 3. Dependencies

- Sports Definition
- Contest Operations
- Live Scoring
- Match Squad/Lineup
- Participant Registry
- Audit

---

# 4. Event Categories

```text
SYSTEM
PERIOD
SCORING
DISCIPLINE
PERSONNEL
PLAY
REVIEW
STOPPAGE
STATISTIC
COMMENTARY
CORRECTION
```

Importance:

```text
KEY
STANDARD
DETAILED
TECHNICAL
```

---

# 5. Core Model

```text
TimelineEvent
TimelineEventActor
TimelineEventRelation
TimelineVersion
```

Recommended fields:

```text
id
tenant_id
contest_id
sequence_number
play_id
category
event_type
event_subtype
period_id
game_clock
elapsed_seconds
primary_participant_id
points_value
home_score_after
away_score_after
description
importance
status
occurred_at
recorded_at
source
provider_name
provider_event_id
supersedes_event_id
reversal_of_event_id
metadata_json
```

---

# 6. Event Status

```text
ACTIVE
PENDING_REVIEW
CONFIRMED
CORRECTED
REVERSED
VOID
```

---

# 7. Business Rules

## RULE-TML-001
Sequence number is authoritative ordering.

## RULE-TML-002
Published events are corrected/reversed, not silently deleted.

## RULE-TML-003
Provider events are normalized before public API exposure.

## RULE-TML-004
Event actor roles must be compatible with event definition.

## RULE-TML-005
Score-after-event should be stored when available.

## RULE-TML-006
Timeline version increments on event create/correct/reverse/reorder.

---

# 8. Commands

```text
timeline.event.create
timeline.event.correct
timeline.event.reverse
timeline.event.void
timeline.event.confirm
timeline.reconcile
```

---

# 9. Queries

```text
Get timeline
Get key events
Get events after sequence
Get events after version
Get events by period
Get event
```

---

# 10. Events

```text
TimelineEventCreated
TimelineEventCorrected
TimelineEventReversed
TimelineVersionChanged
```

Realtime public event names may map to:

```text
contest.timeline.event.created
contest.timeline.event.corrected
contest.timeline.event.reversed
```

---

# 11. UI

Generic timeline shell + sport renderers:

```text
Football
Basketball
Rugby
Cricket
```

Filters:

```text
Key Events
All Events
Scoring
Discipline
Substitutions
```

---

# 12. Errors

```text
TIMELINE_EVENT_INVALID
TIMELINE_SEQUENCE_CONFLICT
TIMELINE_ACTOR_INVALID
TIMELINE_EVENT_ALREADY_REVERSED
TIMELINE_VERSION_STALE
```

---

# 13. Tests

- Goal+assist
- Rugby try+conversion
- Basketball grouped play
- Cricket delivery grouping
- VAR/TMO reversal
- Duplicate event
- Sequence conflict
- Provider normalization
- Reconnect after version

---

# 14. Definition of Done

Complete when all flagship sports can produce one canonical structured timeline usable live and historically.
