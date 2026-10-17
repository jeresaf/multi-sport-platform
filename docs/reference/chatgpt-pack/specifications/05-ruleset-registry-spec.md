# Ruleset Registry — Full Specification

## 1. Purpose

Provide immutable, versioned competition rules that combine:

```text
Sport Definition
+
Competition Format
+
Competition-specific configuration
```

The Ruleset Registry is the policy bridge between generic sports definitions and generic competition structures.

---

# 2. Scope

## In Scope

Rules such as:

- Period duration overrides
- Match duration
- Roster size
- Matchday squad size
- Starter count
- Substitution limits
- Points for win/draw/loss
- Bonus points
- Tie-break order
- Qualification counts
- Extra-time enablement
- Penalty shootout enablement
- Overtime behavior
- Transfer-window behavior
- Eligibility limits
- Age/weight constraints
- Walkover/forfeit behavior
- Result methods
- Competition-specific scoring overrides when explicitly permitted
- Stage-specific rule overrides

## Out of Scope

- Definition of what a goal/try/basket is
- Definition of stage graph itself
- Actual teams/players
- Actual contest status
- Actual results

---

# 3. Core Model

```text
Ruleset
RulesetVersion
RuleValue
StageRuleOverride
EligibilityRule
RankingRule
TieBreakRule
RosterRule
TransferRule
ContestRule
```

Lifecycle:

```text
DRAFT
PUBLISHED
DEPRECATED
ARCHIVED
```

Published ruleset versions are immutable.

---

# 4. Binding Model

Every competition edition should bind:

```text
sport_definition_version_id
competition_format_version_id
ruleset_version_id
```

These bindings form the immutable operational configuration for that edition.

Example:

```text
Kampala Premier League 2026

Sport:
Football Association v3

Format:
Double Round Robin v2

Ruleset:
KPL 2026 v1
```

---

# 5. Rule Categories

## 5.1 Contest Rules

Examples:

```text
regulation_period_duration
clock_behavior
extra_time_enabled
penalties_enabled
overtime_enabled
maximum_overtimes
```

## 5.2 Roster Rules

```text
competition_roster_min
competition_roster_max
match_squad_max
starter_count
bench_max
```

## 5.3 Substitution Rules

```text
substitutions_allowed
substitution_windows
temporary_replacement_enabled
```

## 5.4 Standings Rules

```text
win_points
draw_points
loss_points
bonus_point_rules
```

## 5.5 Tie Break Rules

Ordered list, e.g.:

```text
POINTS
GOAL_DIFFERENCE
GOALS_FOR
HEAD_TO_HEAD
PLAYOFF
```

Sport/format-compatible definitions should be validated.

## 5.6 Qualification Rules

Examples:

```text
top_n_per_group
best_runner_up_count
automatic_qualifiers
next_fastest_count
```

## 5.7 Eligibility Rules

Examples:

```text
minimum_age
maximum_age
weight_class
gender_classification
registration_deadline
required_documents
suspension_check
```

## 5.8 Transfer Rules

Examples:

```text
transfers_enabled
window_start
window_end
max_transfers
loans_enabled
```

---

# 6. Stage-Specific Overrides

Ruleset may override values by stage.

Example Rugby 7s:

```text
Pool Stage
regulation_duration = X

Knockout Stage
regulation_duration = X
extra_time_enabled = true
```

Example Basketball:

```text
Regular Season
best_of = not applicable

Playoffs
series_best_of = 3
```

Stage overrides must reference stages/parameter definitions allowed by the bound format.

---

# 7. Validation

Ruleset publishing requires compatibility validation against:

```text
Sport Definition Version
Competition Format Version
```

Validation examples:

- Cannot enable penalties if sport definition does not support penalty shootout.
- Cannot define group qualification if format has no group stage.
- Cannot set starter count greater than match squad max.
- Cannot enable transfer windows if transfer capability is unsupported/disabled.
- Cannot use unknown tie-break metrics.
- Cannot specify negative scoring values unless explicitly allowed.

---

# 8. Business Rules

## RULE-RLS-001

Published rulesets are immutable.

## RULE-RLS-002

A ruleset version is bound to exactly one compatible sport-definition version and competition-format version combination, unless explicitly designed as a reusable compatible template.

## RULE-RLS-003

All rules must validate before publication.

## RULE-RLS-004

A competition edition must retain the exact bound versions for historical replay.

## RULE-RLS-005

Changing rules for an active edition creates a new ruleset version and requires an explicit controlled rebind policy; silent mutation is prohibited.

## RULE-RLS-006

Ruleset values cannot introduce sport event types not defined by the bound sport definition.

## RULE-RLS-007

Ruleset cannot alter the structural stage graph outside the capabilities exposed by the format version.

---

# 9. Commands

```text
ruleset.create
ruleset.version.create
ruleset.rule.set
ruleset.stage_override.set
ruleset.validate
ruleset.publish
ruleset.deprecate
ruleset.archive
```

---

# 10. Queries

```text
List rulesets
Get ruleset
Get version
Validate compatibility
Resolve effective rule for stage
Get roster rules
Get contest rules
Get standings rules
Get eligibility rules
```

---

# 11. Domain Events

```text
RulesetCreated
RulesetVersionPublished
RulesetDeprecated
RulesetArchived
CompetitionRulesetBound
```

---

# 12. Permissions

```text
ruleset.read
ruleset.create
ruleset.update
ruleset.validate
ruleset.publish
ruleset.deprecate
ruleset.archive
```

Tenant-specific rulesets may require tenant-admin permissions, while platform templates require platform-admin permissions.

---

# 13. Entitlements

Potential commercial capabilities:

```text
CUSTOM_RULESETS
ADVANCED_RULESETS
STAGE_RULE_OVERRIDES
TRANSFER_MANAGEMENT
ADVANCED_ELIGIBILITY
```

---

# 14. Audit

Audit:

- Draft changes
- Compatibility validation
- Publish
- Bind to competition
- Attempted modification of published version
- Deprecation/archive

---

# 15. API Direction

Admin examples:

```text
GET  /api/admin/rulesets
POST /api/admin/rulesets
POST /api/admin/rulesets/{id}/versions
POST /api/admin/ruleset-versions/{id}/validate
POST /api/admin/ruleset-versions/{id}/publish
```

Internal effective rules:

```text
GET /api/internal/competition-editions/{id}/effective-rules
```

Prefer server-side resolved effective rules for operational systems.

---

# 16. UI

Ruleset wizard:

```text
1 Identity
2 Sport Definition
3 Competition Format
4 Contest Rules
5 Rosters
6 Substitutions
7 Standings
8 Tie Breaks
9 Qualification
10 Eligibility
11 Transfers
12 Stage Overrides
13 Validation
14 Publish
```

Provide warnings for unusual but valid configurations.

---

# 17. Seed Rulesets

Flagship examples:

```text
Football League Demo
Football Knockout Demo
Basketball League Demo
Basketball Best-of-3 Demo
Rugby 15s League Demo
Rugby 7s Pools+Knockout Demo
Cricket T20 Demo
Golf Stroke Play Demo
Athletics Meet Demo
MMA Fight Card Demo
```

---

# 18. Acceptance Walkthrough

1. Select Football definition.
2. Select Double Round Robin format.
3. Create ruleset.
4. Set 3/1/0 standings points.
5. Configure roster limits.
6. Configure tiebreak order.
7. Disable extra time in league stage.
8. Validate.
9. Publish.
10. Bind to competition edition.
11. Start edition.
12. Create new ruleset version with changed roster max.
13. Confirm active edition remains on old version.
14. Bind next edition to new version.
15. Verify historical results remain reproducible.
