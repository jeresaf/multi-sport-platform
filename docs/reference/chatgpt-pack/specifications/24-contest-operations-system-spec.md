# Contest Operations System — Full Specification

## 1. Purpose

Own the operational lifecycle of an individual contest from pre-game readiness through live play, interruption, completion, and closure handoff.

---

# 2. Scope

In scope:
- Contest operational state
- Sport phase
- Match readiness
- Check-in
- Start/end
- Clock
- Period changes
- Delay/interruption/suspension
- Abandonment
- Contest status history
- Operational commands
- Official assignment references
- Match squad/lineup validation

Out of scope:
- Schedule ownership
- Scoring event definitions
- Timeline event storage details
- Result approval internals
- Standings progression

---

# 3. Dependencies

- Scheduling
- Ruleset
- Sports Definition
- Match Squad/Lineup
- Official Registry/assignment
- Live Scoring
- Timeline
- Audit

---

# 4. Contest Status

```text
NOT_STARTED
STARTING_SOON
LIVE
DELAYED
INTERRUPTED
SUSPENDED
POSTPONED
ENDED
CLOSED
CANCELLED
ABANDONED
```

Sport phase is separate.

---

# 5. Core Model

```text
Contest
ContestStatusHistory
ContestPeriod
ContestClockState
ContestReadiness
ContestCheckIn
ContestOfficialAssignment
```

---

# 6. Business Rules

## RULE-OPS-001
Contest status and sport phase must remain separate.

## RULE-OPS-002
Contest cannot start unless mandatory readiness checks pass or authorized override occurs.

## RULE-OPS-003
Status transitions must follow validated state machine.

## RULE-OPS-004
Exceptional states require reason.

## RULE-OPS-005
ENDED means sporting action finished; CLOSED requires approved result.

## RULE-OPS-006
Clock/period behavior comes from Sport Definition + Ruleset.

## RULE-OPS-007
Administrative status reversal requires audit and permission.

---

# 7. Commands

```text
contest.check_in
contest.mark_starting_soon
contest.start
contest.period.start
contest.period.end
contest.clock.start
contest.clock.stop
contest.interrupt
contest.resume
contest.suspend
contest.postpone
contest.end
contest.abandon
contest.cancel
contest.close
contest.reopen
```

---

# 8. Queries

```text
Get contest
Get readiness
Get status
Get phase
Get clock
Get periods
Get status history
Get officials
```

---

# 9. Events

```text
ContestStartingSoon
ContestStarted
ContestPeriodChanged
ContestInterrupted
ContestResumed
ContestSuspended
ContestEnded
ContestAbandoned
ContestClosed
```

---

# 10. UI

Match Operations dashboard:

```text
Score/Status Hero
Readiness
Clock/Period
Lineups
Officials
Timeline
Scoring Controls
Audit
```

---

# 11. Errors

```text
CONTEST_INVALID_TRANSITION
CONTEST_NOT_READY
CONTEST_ALREADY_STARTED
CONTEST_ALREADY_ENDED
CONTEST_REASON_REQUIRED
CONTEST_RULESET_MISSING
```

---

# 12. Tests

- State transitions
- Invalid transitions
- Readiness
- Clock
- Periods
- Interrupt/resume
- Suspend
- Abandon
- Close after approved result

---

# 13. Definition of Done

Complete when every contest has one auditable, ruleset-driven operational lifecycle independent of schedule and result approval.
