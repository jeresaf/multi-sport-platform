# Competition Format System — Full Specification

## 1. Purpose

Define reusable, versioned competition structures independently of sport scoring rules.

The system answers:

> How is this competition organised?

It does not answer:

> How does the sport score?

---

# 2. Scope

## In Scope

- Format identity
- Versioning
- Stage templates
- Group/pool structures
- Round templates
- Elimination structures
- Series structures
- Heat/progression structures
- Leaderboard-stage structures
- Fight-card structures
- Fixture-generator capabilities
- Progression graph
- Placement/classification paths
- Third-place configuration capability
- Format parameters

## Out of Scope

- Goals/tries/baskets
- Sport-specific periods
- Competition-specific tiebreak order
- Actual participants
- Actual fixtures
- Actual results
- Actual standings

---

# 3. Canonical Built-In Formats

Initial built-in formats:

```text
SINGLE_ROUND_ROBIN
DOUBLE_ROUND_ROBIN
MULTI_ROUND_ROBIN
SINGLE_ELIMINATION
TWO_LEG_ELIMINATION
GROUP_PLUS_KNOCKOUT
POOL_PLUS_KNOCKOUT
BEST_OF_SERIES
HEAT_TO_FINAL
MULTI_ROUND_LEADERBOARD
FIGHT_CARD
```

Future:

```text
SWISS
CONFERENCE_PLAYOFF
STAGE_RACE
CUSTOM_GRAPH
```

---

# 4. Core Model

```text
CompetitionFormat
CompetitionFormatVersion
StageTemplate
GroupTemplate
RoundTemplate
ProgressionRuleTemplate
FixtureGeneratorDefinition
FormatParameterDefinition
```

Published versions are immutable.

Lifecycle:

```text
DRAFT
PUBLISHED
DEPRECATED
ARCHIVED
```

---

# 5. Stage Template

Fields:

```text
code
name
stage_type
sequence
repeatable
participant_input
participant_output
```

Stage types:

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

# 6. Progression Graph

Formats should support explicit progression.

Example:

```text
GROUP_A ─┐
GROUP_B ─┼→ QUARTERFINAL → SEMIFINAL → FINAL
GROUP_C ─┤
GROUP_D ─┘
```

Progression edges define:

```text
source_stage
qualification_selector
target_stage
target_slot
```

The actual qualification rule values are resolved through the bound ruleset.

---

# 7. Format Parameters

Examples:

Round robin:

```text
cycles
home_away
balanced_schedule
```

Knockout:

```text
number_of_legs
third_place_match
reseed_each_round
```

Best of series:

```text
best_of
home_pattern
```

Group/pool:

```text
group_count
participants_per_group
qualifiers_per_group
best_runner_up_count
```

Heat:

```text
heat_count
automatic_qualifiers
next_fastest_count
```

Fight card:

```text
sections
ordering
main_event_required
```

---

# 8. Fixture Generator Capability

A format version may declare:

```text
AUTO_GENERATE
PARTIAL_GENERATE
MANUAL_ONLY
```

Generated schedules create canonical contest records through Scheduling; the format system itself does not own scheduled dates/venues.

---

# 9. Business Rules

## RULE-FMT-001

Published format versions are immutable.

## RULE-FMT-002

A format must have a valid connected stage/progression structure.

## RULE-FMT-003

The format must not contain sport-specific scoring-event definitions.

## RULE-FMT-004

Format parameter definitions must include validation type/range.

## RULE-FMT-005

A format used by historical competitions cannot be deleted.

## RULE-FMT-006

Format progression must be deterministic or explicitly declare manual resolution points.

---

# 10. Commands

```text
competition_format.create
competition_format.update
competition_format.version.create
competition_format.stage.add
competition_format.progression.add
competition_format.publish
competition_format.deprecate
competition_format.archive
```

---

# 11. Queries

```text
List formats
Get format
List versions
Get stage template
Get progression graph
Get generator capabilities
Get parameter schema
```

---

# 12. Domain Events

```text
CompetitionFormatCreated
CompetitionFormatVersionPublished
CompetitionFormatVersionDeprecated
CompetitionFormatVersionArchived
```

---

# 13. Permissions

```text
competition_format.read
competition_format.create
competition_format.update
competition_format.publish
competition_format.deprecate
competition_format.archive
```

---

# 14. Integrity

- Unique format code
- Unique version per format
- Immutable published version
- No broken progression edges
- No orphan stages
- No hard deletion if referenced

---

# 15. UI

Admin screens:

```text
Formats
Format Detail
Versions
Visual Stage Builder
Progression Graph
Parameters
Fixture Generation
Validation
Publish Review
```

The progression UI should be visual where practical but serialize to a deterministic canonical model.

---

# 16. Seed

Built-in format seeds should be versioned and idempotent.

Minimum E2E examples:

- Football league
- Football knockout
- Football group+knockout
- Basketball league/playoffs
- Rugby 7s pools+knockout
- Cricket group+knockout
- Golf multi-round leaderboard
- Athletics heat-to-final
- MMA fight card

---

# 17. Acceptance Walkthrough

1. Create round-robin format version.
2. Add regular-season stage.
3. Configure generator capability.
4. Publish.
5. Create group+knockout format.
6. Add group stage.
7. Add knockout stage.
8. Connect qualification progression.
9. Validate.
10. Publish.
11. Bind format to competition edition through Ruleset.
12. Instantiate stages.
13. Generate fixtures where supported.
14. Confirm new format version does not change existing edition.
