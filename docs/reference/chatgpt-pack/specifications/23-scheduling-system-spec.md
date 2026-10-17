# Scheduling System — Full Specification

## 1. Purpose

Own when and where contests are planned to occur, including fixture generation, manual scheduling, venue assignment, publication, rescheduling, postponement, and schedule history.

---

# 2. Scope

In scope:
- Fixture generation requests
- Contest schedule records
- Date/time
- Venue
- Round/stage placement
- Publication
- Reschedule
- Postpone scheduling
- Conflict detection
- Official assignment availability input
- Schedule history
- Bulk scheduling

Out of scope:
- Live contest lifecycle
- Score
- Timeline
- Result approval
- Stage-template definition

---

# 3. Dependencies

- Competition Edition
- Stage Instantiation
- Competition Format
- Ruleset Registry
- Participant Registry
- Venue Registry
- Official Registry
- Audit

---

# 4. Core Model

```text
ScheduledContest
ContestScheduleVersion
ScheduleChange
SchedulePublication
ScheduleConflict
```

Important: the canonical `Contest` may be created by scheduling but operational status belongs to Contest Operations.

---

# 5. Schedule Status

```text
DRAFT
PUBLISHED
RESCHEDULED
POSTPONED
CANCELLED
```

Do not reuse contest live status for schedule status.

---

# 6. Business Rules

## RULE-SCH-001
Approved/published schedule changes create history; do not overwrite silently.

## RULE-SCH-002
Date/time stored with timezone-safe representation.

## RULE-SCH-003
Venue conflict checks required when venue assigned.

## RULE-SCH-004
Participant conflict checks required for overlapping contests.

## RULE-SCH-005
Official availability may be checked but official assignment is separate relationship.

## RULE-SCH-006
Fixture generation must conform to instantiated format structure.

## RULE-SCH-007
Manual scheduling is allowed where format permits or generator unavailable.

---

# 7. Commands

```text
schedule.generate
schedule.contest.create
schedule.assign_time
schedule.assign_venue
schedule.publish
schedule.reschedule
schedule.postpone
schedule.cancel
schedule.bulk_assign
```

---

# 8. Queries

```text
Get schedule
List contests by date
List by round/stage
Get schedule conflicts
Get venue schedule
Get team/participant schedule
Get unpublished fixtures
```

---

# 9. Events

```text
FixturesGenerated
SchedulePublished
ContestScheduled
ContestRescheduled
ContestPostponed
ContestCancelledBySchedule
```

---

# 10. Permissions

```text
fixture.generate
fixture.create
fixture.schedule
fixture.reschedule
fixture.postpone
fixture.cancel
fixture.publish
```

---

# 11. UI

Schedule Calendar  
Fixture List  
Round Scheduler  
Venue Planner  
Conflict Panel  
Bulk Actions  
Schedule History

---

# 12. Notifications

Possible:
- fixture published
- rescheduled
- postponed
- cancelled
- venue changed

---

# 13. Errors

```text
SCHEDULE_CONFLICT_PARTICIPANT
SCHEDULE_CONFLICT_VENUE
SCHEDULE_INVALID_STAGE
SCHEDULE_ALREADY_PUBLISHED
SCHEDULE_CHANGE_REASON_REQUIRED
```

---

# 14. Tests

- Auto generation
- Manual fixture
- Conflict detection
- Reschedule history
- Postpone
- Publish
- Tenant isolation

---

# 15. Definition of Done

Complete when all competition fixtures can be generated or manually created, safely published, and historically rescheduled without owning live game state.
