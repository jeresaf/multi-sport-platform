# Sport definitions — reference files

These YAML files are the **reference source** for the five flagship sport variants (ADR-0003, ADR-0006). In S05
the backend gets its own seed copies (e.g. `backend/src/Modules/SportsConfiguration/.../Definitions/*.yaml`),
validated by a JSON Schema, versioned and hashed. When the backend copy and these files diverge, the PR must say
why and update both.

| File | Variant | Family |
|---|---|---|
| football.yaml | `FOOTBALL_ASSOCIATION` | ADDITIVE |
| rugby-union-15.yaml | `RUGBY_UNION_15` | ADDITIVE |
| rugby-union-7.yaml | `RUGBY_UNION_7` | ADDITIVE |
| basketball.yaml | `BASKETBALL_STANDARD` (FIBA-style quarters) | ADDITIVE |
| cricket-t20.yaml | `CRICKET_T20` | INNINGS |

> **Accuracy note.** Values follow widely used international rules (IFAB Laws of the Game, FIBA, World Rugby,
> MCC Laws / common T20 playing conditions) as understood when written. They are **defaults**: the edition's
> ruleset decides the values a competition uses, and a domain expert should review each file before real
> competitions rely on it (docs/build/open-questions.md Q9).

## Schema (v1)

```yaml
schema_version: 1
variant: FOOTBALL_ASSOCIATION       # unique code; never reused
sport: FOOTBALL
version: 1                          # immutable once published (content hash checked in CI)
name: Football
scoring_family: ADDITIVE            # ADDITIVE | INNINGS | SETS | RANKED_METRIC | COMBAT
shape: HEAD_TO_HEAD                 # HEAD_TO_HEAD | FIELD
participant_types: [TEAM]           # TEAM | INDIVIDUAL | PAIR | ...
sides: [HOME, AWAY]                 # HEAD_TO_HEAD only
coverage_levels: [RESULT_ONLY, BASIC, STANDARD, DETAILED]
terminology: { contest: Match, contest_plural: Matches, period: Half, participant: Team, athlete: Player, score_unit: Goal }

supports:                           # what rulesets may enable for this sport
  tie_resolutions: [DRAW, EXTRA_PERIODS_THEN_SHOOTOUT, SHOOTOUT]

periods:                            # ordered; codes unique within the definition
  - code: FIRST_HALF
    label: 1st half
    short: 1H
    kind: PLAY                      # PLAY | BREAK | SHOOTOUT
    clock: COUNT_UP                 # COUNT_UP | COUNT_DOWN | NONE
    default_seconds: 2700           # rules may override (contest.period_seconds.<CODE>)
    clock_starts_at_seconds: 0      # display offset (second half shows 45:00)
    allows_added_time: true
    overtime: false                 # true for extra periods
    repeatable: false               # true = may occur many times (basketball overtime)
    sudden_death: false             # true = first score in this period ends the contest

actor_roles:                        # who may fill a role on an event
  SCORER: { who: ON_FIELD, side: OWN }
  # who: ON_FIELD | BENCH | SQUAD | STAFF | SQUAD_OR_STAFF ; side: OWN | OPPONENT

event_types:
  - code: GOAL
    category: SCORING               # SYSTEM | PERIOD | SCORING | DISCIPLINE | PERSONNEL | PLAY | REVIEW | STOPPAGE | STATISTIC | COMMENTARY | CORRECTION
    importance: KEY                 # KEY | STANDARD | DETAILED | TECHNICAL
    coverage: BASIC                 # lowest coverage level whose scorer pad offers it
    label: Goal
    points: 1                       # ADDITIVE only
    side: OWN                       # which side receives the points: OWN | OPPONENT
    component: main                 # score component (main | shootout …)
    phases: [PLAY]                  # allowed phase kinds
    actors:
      - { role: SCORER, required_from: STANDARD }   # required at STANDARD+, optional below
      - { role: ASSIST, optional: true, distinct_from: SCORER }
    metadata:                       # JSON-schema-like, validated on record
      body_part: { type: enum, values: [RIGHT_FOOT, LEFT_FOOT, HEAD, OTHER], optional: true }
    effects: []                     # SEND_OFF | TEMPORARY_SUSPENSION | SUBSTITUTE | TEMPORARY_SUBSTITUTE | RETURN | DISQUALIFY | FOUL_COUNT
    follow_up: null                 # e.g. conversion requires a preceding, unconsumed try (see rugby)
    limits: []                      # rule-driven limits, e.g. [{ rule: substitutions.max }]
    reversible_by: [VAR_DECISION]   # review events allowed to reverse it (plus generic EVENT_REVERSED)

positions:      [ { code: GK, label: Goalkeeper, group: GOALKEEPER } ]
staff_roles:    [ { code: HEAD_COACH, label: Head coach } ]
official_roles: [ { code: REFEREE, label: Referee, default_minimum: 1 } ]

lineup:                             # defaults; ruleset overrides numbers
  starters: 11
  bench_max: 9
  position_constraints:             # by position group or single position code
    - { group: GOALKEEPER, min: 1, max: 1 }
  special_roles: [CAPTAIN]

statistics:
  - code: GOALS
    label: Goals
    scopes: [PLAYER, TEAM]
    aggregation: SUM                # SUM | MAX | MIN | RATIO | BUILTIN
    higher_is_better: true
    coverage: BASIC
    derive: { count_events: [GOAL, PENALTY_GOAL], actor_roles: [SCORER, TAKER] }
    # other derive forms: { count_events: [...], actor_roles: [...], where: { metadata_field: value } }
    #                     { sum_points: [TRY, CONVERSION_MADE], actor_roles: [SCORER, KICKER] }
    #                     { builtin: MINUTES_PLAYED }  ·  { manual: true }

scorer_pad:                         # ADDITIVE uses BUTTONS; INNINGS uses DELIVERY_PAD (family component)
  kind: BUTTONS
  groups:
    - { label: Score, buttons: [GOAL, PENALTY_GOAL, OWN_GOAL] }
```

## Generic event types (available in every definition, not repeated in files)

| Code | Category | Purpose |
|---|---|---|
| `PERIOD_START`, `PERIOD_END` | PERIOD | Start/end a period from the definition (`period_code`, optional `reason`) — not used by INNINGS, which has `INNINGS_START`/`INNINGS_END` |
| `CLOCK_STOP`, `CLOCK_START`, `CLOCK_SET` | SYSTEM | Clock control; `CLOCK_SET` needs a reason |
| `STATUS_CHANGED` | SYSTEM | Written automatically when contest status changes (delay, interruption, suspension, abandonment) so the timeline tells the full story |
| `EVENT_REVERSED` | CORRECTION | Generic reversal ("entered in error") with `reverses_event_id` + reason |
| `NOTE` | COMMENTARY | Free-text commentary line; never affects score or statistics |

## Conventions

- Codes are UPPER_SNAKE and stable. Labels are English display text (i18n later).
- Never put competition policy (points for a win, roster sizes, tiebreakers) in a definition — that's the
  ruleset (docs/domain/rulesets.md). Definitions say what *can* happen; rulesets say what *this competition*
  uses.
- Statistics marked `builtin` are computed by family-specific code in Insights (e.g. minutes played from the
  personnel tracker, cricket economy rate); everything else is derived generically from timeline events of
  approved contests.
- Attribution: for `TEAM` scope an event counts for the acting participant regardless of actors; for `PLAYER`
  scope it counts only for the athlete filling one of the listed `actor_roles` (so a penalty try, which has no
  actor, counts for the team but no player).
- Injury events record only that a stoppage happened — never medical details (ADR-0015).
