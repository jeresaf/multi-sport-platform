# Sports Definition System — Full Specification

## 1. System Identity

**Name:** Sports Definition System  
**Short Code:** `SPORTS_DEF`

### Purpose

Provide the canonical, versioned definition of each sport and sport variant supported by the platform so that competition, scoring, timeline, UI, statistics, and official-role behavior can be configured instead of hard-coded.

### Business Outcome

A platform administrator can define a sport once and allow multiple tenants/competitions to use a stable version of that definition without adding sport-specific conditionals throughout the core platform.

---

# 2. Scope

## In Scope

- Sport identity
- Sport variants
- Definition versions
- Participant structures
- Contest shape
- Period/segment definitions
- Scoring-action definitions
- Timeline-event definitions
- Player position definitions
- Team/staff role definitions
- Official-role definitions
- Statistic definitions
- Presentation capability metadata
- Default coverage capabilities
- Validation metadata
- Activation/deprecation/archive lifecycle

## Out of Scope

- Competition structure such as knockout/round robin
- Competition-specific points tables
- Competition-specific tiebreakers
- Competition rosters
- Actual players
- Actual teams
- Fixtures
- Live contest state
- Actual scores/results
- Tenant subscriptions

---

# 3. Ubiquitous Language

| Term | Definition |
|---|---|
| Sport | A broad sporting discipline such as Football, Basketball, Rugby, Cricket |
| Sport Variant | A distinct rules/presentation form under a sport, e.g. Rugby 7s or Rugby 15s |
| Sport Definition Version | Immutable published configuration describing how the sport behaves |
| Contest Shape | Structural participant model such as head-to-head or multi-participant |
| Period Definition | Configurable segment of play such as half, quarter, round, innings |
| Scoring Action | Event capable of affecting score/result |
| Timeline Event Definition | Allowed structured event type in play-by-play |
| Statistic Definition | Metric available for players, teams or contests |
| Position Definition | Sport-specific athlete/player position |
| Official Role | Sport-specific official role applicable to contests |

---

# 4. Actors

## Platform Sports Administrator

Can create and publish sport definitions.

## Platform Administrator

Can activate/deprecate definitions and review usage.

## Tenant Administrator

Can view enabled definitions but cannot mutate platform-global definitions unless custom-sport capability exists.

## System / Seeder

Can bootstrap approved built-in sport definitions.

---

# 5. Dependencies

## Upstream

- Reference Data Registry
- Audit
- Data Governance

## Downstream

- Ruleset Registry
- Competition Management
- Contest Operations
- Timeline
- Statistics
- Public/Admin presentation
- Seed/Test tooling

---

# 6. Ownership

## Source-of-Truth Entities

```text
Sport
SportVariant
SportDefinitionVersion
ContestShapeDefinition
PeriodDefinition
ScoringActionDefinition
TimelineEventDefinition
PositionDefinition
StaffRoleDefinition
OfficialRoleDefinition
StatisticDefinition
SportPresentationDefinition
SportCoverageDefinition
```

## Referenced External IDs

- Reference-data identifiers where applicable
- Tenant identifier only for future tenant-defined custom sports

---

# 7. Core Entity Model

## Sport

Fields:

```text
id
code
name
short_name
description
status
is_platform_builtin
created_at
created_by
```

Constraints:

- `code` globally unique
- published sport cannot be hard-deleted

## SportVariant

Examples:

```text
FOOTBALL_ASSOCIATION
BASKETBALL_STANDARD
RUGBY_UNION_15
RUGBY_UNION_7
CRICKET_T20
```

Fields:

```text
id
sport_id
code
name
description
status
```

## SportDefinitionVersion

Fields:

```text
id
sport_variant_id
version_number
status
effective_from
published_at
published_by
change_summary
definition_hash
```

Lifecycle:

```text
DRAFT
PUBLISHED
DEPRECATED
ARCHIVED
```

Published versions are immutable.

---

# 8. Contest Shape

Canonical shapes:

```text
HEAD_TO_HEAD_TEAM
HEAD_TO_HEAD_INDIVIDUAL
MULTI_PARTICIPANT_RANKED
RACE
HEAT
SERIES
FIGHT_CARD_BOUT
COMPLEX_SCORE
```

Sport variant declares one or more supported shapes.

Example:

```text
FOOTBALL_ASSOCIATION
→ HEAD_TO_HEAD_TEAM

GOLF_STROKE_PLAY
→ MULTI_PARTICIPANT_RANKED

ATHLETICS_TRACK
→ HEAT / RACE
```

---

# 9. Period Definitions

A version defines supported/default periods.

Common fields:

```text
code
display_name
sequence
duration_seconds
clock_direction
is_break
is_overtime
repeatable
max_repetitions
```

Examples:

Football:

```text
FIRST_HALF
HALFTIME
SECOND_HALF
EXTRA_TIME_FIRST_HALF
EXTRA_TIME_BREAK
EXTRA_TIME_SECOND_HALF
PENALTIES
```

Basketball:

```text
Q1
BREAK
Q2
HALFTIME
Q3
BREAK
Q4
OVERTIME
```

Rugby:

```text
FIRST_HALF
HALFTIME
SECOND_HALF
EXTRA_TIME
```

Cricket T20:

```text
FIRST_INNINGS
INNINGS_BREAK
SECOND_INNINGS
SUPER_OVER
```

Default durations may be overridden by a ruleset where allowed.

---

# 10. Scoring Actions

Fields:

```text
code
display_name
category
default_points
affects_primary_score
affects_secondary_score
requires_team
requires_primary_actor
allows_secondary_actor
allowed_periods
metadata_schema
```

Examples:

Football:

```text
GOAL
OWN_GOAL
PENALTY_GOAL
SHOOTOUT_GOAL
```

Basketball:

```text
FREE_THROW_MADE
TWO_POINT_MADE
THREE_POINT_MADE
```

Rugby:

```text
TRY
PENALTY_TRY
CONVERSION_MADE
PENALTY_GOAL
DROP_GOAL
```

Cricket scoring uses richer score-state metadata and must support complex scoring.

---

# 11. Timeline Event Definitions

Canonical categories:

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

Definition fields:

```text
event_code
category
importance
public_label
affects_score
requires_participant
requires_primary_actor
allows_secondary_actor
supports_coordinates
supports_correction
metadata_schema
```

Timeline definitions are sport-configurable while the timeline engine remains generic.

---

# 12. Player Positions

Examples:

Football:

```text
GK
DF
MF
FW
```

Basketball:

```text
PG
SG
SF
PF
C
```

Rugby 15s can include sport-specific numbered/role positions.

Definition fields:

```text
code
name
short_name
display_order
category
active
```

---

# 13. Staff Roles

Examples:

```text
HEAD_COACH
ASSISTANT_COACH
TEAM_MANAGER
PHYSIO
DOCTOR
EQUIPMENT_MANAGER
```

Sport definitions may declare allowed roles but Person/Staff Registry owns actual people.

---

# 14. Official Roles

Examples:

Football:

```text
REFEREE
ASSISTANT_REFEREE
FOURTH_OFFICIAL
MATCH_COMMISSIONER
```

Basketball:

```text
REFEREE
UMPIRE
SCORER
TIMEKEEPER
```

Rugby:

```text
REFEREE
ASSISTANT_REFEREE
TMO
COMMISSIONER
```

Cricket:

```text
UMPIRE
THIRD_UMPIRE
MATCH_REFEREE
SCORER
```

---

# 15. Statistic Definitions

Fields:

```text
code
name
scope
data_type
aggregation
higher_is_better
public_default
requires_coverage
```

Scopes:

```text
CONTEST
TEAM
PLAYER
SEASON
CAREER
```

Examples:

Football:

```text
GOALS
ASSISTS
SHOTS
POSSESSION
```

Basketball:

```text
POINTS
REBOUNDS
ASSISTS
STEALS
BLOCKS
```

Rugby:

```text
TRIES
CONVERSIONS
PENALTY_GOALS
```

Cricket:

```text
RUNS
WICKETS
STRIKE_RATE
ECONOMY
```

---

# 16. Presentation Definition

Sport definition may specify presentation capabilities, not exact tenant branding.

Fields can include:

```text
default_renderer_kind
score_display_strategy
clock_display_strategy
supports_lineups
supports_box_score
supports_timeline
supports_standings
supports_leaderboard
supports_bracket
```

Examples:

```text
Football → HEAD_TO_HEAD
Golf → MULTI_PARTICIPANT
Cricket → COMPLEX_SCORE
```

---

# 17. Coverage Definition

Default capabilities:

```text
timeline
key_events
full_play_by_play
player_attribution
assists
coordinates
statistics
commentary
lineups
```

Competition/contest may downgrade actual coverage.

---

# 18. State Machine

```text
DRAFT
  ↓ publish
PUBLISHED
  ↓ deprecate
DEPRECATED
  ↓ archive
ARCHIVED
```

Rules:

- Draft can be edited.
- Published is immutable.
- Deprecated can still be used by existing competitions.
- Archived remains historically readable.
- New competition editions should not bind archived definitions.

---

# 19. Business Rules

## RULE-SPT-001

A published definition version is immutable.

## RULE-SPT-002

A sport code is globally unique.

## RULE-SPT-003

A variant code is unique within the sport.

## RULE-SPT-004

A definition cannot publish without at least one contest shape.

## RULE-SPT-005

Every scoring action that affects score must define its score behavior or metadata contract.

## RULE-SPT-006

Every referenced period/event/statistic code must be unique within a definition version.

## RULE-SPT-007

Deleting a definition used historically is prohibited; deprecate/archive instead.

## RULE-SPT-008

Competition Format does not own sport event/scoring definitions.

---

# 20. Commands

Suggested actions:

```text
sport.create
sport.update
sport.variant.create
sport.definition.create_draft
sport.definition.publish
sport.definition.deprecate
sport.definition.archive
sport.period.add
sport.scoring_action.add
sport.timeline_event.add
sport.position.add
sport.statistic.add
sport.official_role.add
```

Each command requires permission, audit, validation, and optimistic concurrency where relevant.

---

# 21. Queries

Required:

```text
List sports
Get sport
List variants
Get definition version
Get latest published version
List scoring actions
List timeline events
List positions
List statistics
List official roles
Get presentation capabilities
```

---

# 22. Domain Events

Examples:

```text
SportCreated
SportVariantCreated
SportDefinitionPublished
SportDefinitionDeprecated
SportDefinitionArchived
```

`SportDefinitionPublished` should include:

```text
sport_id
variant_id
version_id
version_number
definition_hash
published_at
```

---

# 23. Permissions

```text
sport.read
sport.create
sport.update
sport.variant.manage
sport.definition.create
sport.definition.publish
sport.definition.deprecate
sport.definition.archive
```

---

# 24. Entitlements

Platform built-in sports may be selectable through tenant entitlements.

Possible capabilities:

```text
SPORTS_CONFIGURATION
CUSTOM_SPORTS
MAX_CUSTOM_SPORTS
```

A tenant may be entitled to use a sport without permission to alter its definition.

---

# 25. Audit

Audit:

- Sport create/update
- Variant create/update
- Draft definition changes
- Publish
- Deprecate
- Archive
- Import
- Failed publish validation

Published definition hash should be included in audit metadata.

---

# 26. Data Integrity

- Globally unique sport codes
- Unique variant code per sport
- Unique version number per variant
- Immutable published versions
- Referential constraints from definitions to sport/variant
- No hard delete once referenced
- Definition content can optionally be stored as structured relational entities plus canonical serialized snapshot/hash

---

# 27. Import / Export

Support:

- JSON definition package
- CSV/Excel for definition sub-tables where useful

Import flow:

```text
Upload
→ Parse
→ Validate
→ Preview
→ Duplicate/version check
→ Create Draft
→ Explicit Publish
```

Imports must never auto-overwrite a published version.

---

# 28. API Contract Direction

Admin examples:

```text
GET    /api/admin/sports
POST   /api/admin/sports
GET    /api/admin/sports/{id}
POST   /api/admin/sports/{id}/variants
POST   /api/admin/sport-definitions
POST   /api/admin/sport-definitions/{id}/publish
POST   /api/admin/sport-definitions/{id}/deprecate
```

Internal canonical read:

```text
GET /api/internal/sport-definitions/{variantId}/versions/{versionId}
```

Public clients generally should consume competition presentation models rather than raw configuration.

---

# 29. Admin UI

Main screens:

```text
Sports
Sport Detail
Variants
Definition Versions
Definition Editor
Periods
Scoring
Timeline Events
Positions
Officials
Statistics
Presentation
Coverage
Validation
Publish Review
```

Definition editor should use tabs/stepper and show validation errors before publish.

---

# 30. Seed Data

Initial built-in definitions:

```text
Football
Basketball
Rugby Union 15s
Rugby Union 7s
Cricket T20
Golf
Athletics
MMA
Boxing
Volleyball
Netball
Hockey
Tennis
```

Flagship definitions should be comprehensive first.

---

# 31. Error Catalogue

Examples:

```text
SPORT_CODE_EXISTS
SPORT_VARIANT_CODE_EXISTS
SPORT_VERSION_IMMUTABLE
SPORT_DEFINITION_INVALID
SPORT_DEFINITION_IN_USE
SPORT_DEFINITION_ALREADY_PUBLISHED
SPORT_DEFINITION_ARCHIVED
```

---

# 32. Tests

Required:

- Definition validation
- Version immutability
- Duplicate codes
- Publish/deprecate/archive transitions
- Tenant entitlement access
- Scoring-action validation
- Period validation
- Event definition validation
- Import dry run
- Seed idempotency
- Existing competition remains bound to old version after new version publishes

---

# 33. Acceptance Walkthrough

1. Create sport.
2. Create variant.
3. Create draft definition.
4. Add contest shape.
5. Add periods.
6. Add scoring actions.
7. Add timeline events.
8. Add positions.
9. Add officials.
10. Add statistics.
11. Validate.
12. Publish version 1.
13. Bind version to test competition.
14. Create version 2.
15. Modify draft.
16. Publish version 2.
17. Confirm existing competition remains on version 1.
18. Confirm new competition can select version 2.
19. Deprecate version 1.
20. Confirm historical read still works.

---

# 34. Definition of Done

Complete when the platform can add or revise a sport definition through versioned configuration without changing generic competition, contest, timeline, or UI architecture.
