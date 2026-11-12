# Ruleset templates, parameters and tiebreakers

Owner: Sports Configuration (templates, resolver), Competitions (edition snapshot). Decision: ADR-0006.

A **ruleset template** combines a sport variant and a format with competition policy (points, roster sizes,
tiebreakers, periods…). Tenants pick a template in the competition wizard and adjust only the parameters it
exposes. The resolver validates the result against the sport definition and format, and the edition freezes it
as an immutable **effective rules snapshot**.

## Template file (repo, versioned)

```yaml
code: FOOTBALL_LEAGUE
version: 1
sport_variant: FOOTBALL_ASSOCIATION
formats: [ROUND_ROBIN]
editable:                       # parameters tenants may change, with bounds
  standings.points.win:   { min: 1, max: 5 }
  standings.tiebreakers:  { allowed: [POINTS, GOAL_DIFFERENCE, GOALS_FOR, WINS, H2H_POINTS, H2H_DIFFERENCE, H2H_SCORED, DRAWING_OF_LOTS] }
  contest.period_seconds.FIRST_HALF:  { min: 600, max: 2700 }
  contest.period_seconds.SECOND_HALF: { min: 600, max: 2700 }
  lineup.bench_max:       { min: 0, max: 15 }
  substitutions.max:      { min: 0, max: 15 }
  roster.min:             { min: 11, max: 40 }
  roster.max:             { min: 11, max: 60 }
values: { … defaults, see snapshot example … }
```

Parameter types: `int`, `decimal`, `bool`, `enum`, `duration_seconds`, ordered `list<enum>`, `map<period,
seconds>`. Validation examples: starters ≤ squad size; penalties/shootout only if the sport defines a shootout
period; group qualification only if the format has groups; tiebreakers must be known to the catalogue and valid
for the sport (e.g. `TRIES_SCORED` only for rugby); negative points only with an explicit flag.

**As built (S05-T4a).** The files are `football-league.yaml` and `football-cup.yaml`, embedded in Sports
Configuration under `Definitions/Rulesets` with `ruleset-template.schema.json`. Each file has `schema_version` (1),
`code`, `version`, `name`, an optional `description`, `sport: { variant, version }` (the template pins the sport
definition's version, whose period, position and official codes it uses), `formats` (codes; `FOOTBALL_LEAGUE` takes
`ROUND_ROBIN` and `SINGLE_CONTEST`, `FOOTBALL_CUP` takes `KNOCKOUT` and `SINGLE_CONTEST`; owner, S05-T4 plan),
`editable` and `values`. `values` holds the sections of the snapshot example below (`contest`, `lineup`,
`substitutions`, `roster`, `standings`, `operations`, `eligibility`) with exactly those keys; the closed vocabularies
hold only the values shown (`lock_policy: FIRST_CONTEST`, `readiness`, `minor_consent_required: TENANT_DEFAULT`), and
the slices that use them add more with a schema change. `editable` is keyed by the dotted path into `values`; a
parameter has `type` (`int` with `min` and `max`, `bool`, `enum` or `list` with `values`), `label` and an optional
`description`; its default is the value at its path (written once). The parameter types built are `int`, `bool`,
`enum` and the ordered `list` of codes (each once, any subset of `values`); a period's length is one `int` path per
period (`contest.period_seconds.FIRST_HALF`); `decimal`, `duration_seconds` and the period map come with a template
that needs them. Rules in code (`RulesetTemplateRules`, `RulesetValueChecks`): the pinned sport version exists in the
embedded files; every format exists and takes the sport's contest shape; an editable path names a value of its type,
inside its bounds and among its values (the schema keeps a list's codes distinct), only `standings.tiebreakers`,
`contest.tie_resolution` and `contest.coverage_default` may be choices or lists, and the codes a tenant may pick are
ones the sport supports; the periods, extra periods, positions (code or group), official roles and default coverage are the
definition's (an extra period once, each an `overtime` period, or first the break the definition lists right before one, at most one `SHOOTOUT` period and it comes last); the tie resolution is one the sport supports, `DRAW` plays no extra periods, any other needs them and a
shoot-out needs a `SHOOTOUT` period, `SHOOTOUT` alone plays no extra period of play and every other resolution plays one;
a tie resolution a tenant may pick must also fit the template's extra periods (a tenant can't change them); ranges are in order; a position minimum fits the starters; `roster.min` holds the
starters and `roster.max` the starters and the bench; tiebreakers are in the catalogue, apply to the sport and appear
once. The tiebreaker catalogue is `Definitions/Rulesets/tiebreakers.yaml` (`tiebreakers.schema.json`): code, label,
description and an optional `sports` list (a definition's `sport`) that limits it (`TRIES_SCORED`, `TRY_DIFFERENCE`
for rugby union, `NET_RUN_RATE` for cricket); embedded and checked at startup, not seeded (owner, S05-T4 plan);
`FAIR_PLAY` comes with its card points. Seeded, hashed and immutable like formats (`config.ruleset_templates`, with the
sport variant, sport version and formats as columns too; `RULESET_TEMPLATE_IMMUTABLE`); read with `ruleset.read`.
Because a template pins its sport version and the files hold one version per sport, a new sport version needs new
versions of its templates in the same change (owner, review #84 Q1); stored templates keep resolving against the
version they pin, which the resolver reads from the stored definitions. A template's `lineup` numbers override the
definition's lineup defaults (docs/domain/sports/README.md): starters may differ from the definition's, as in
small-sided school football, and contests read them from the snapshot, never from the definition (owner, review #84 Q2).

## Effective rules snapshot (example: football league)

```json
{
  "sport":    { "variant": "FOOTBALL_ASSOCIATION", "version": 1, "hash": "sha256:…" },
  "format":   { "code": "ROUND_ROBIN", "version": 1, "parameters": { "cycles": 2, "home_away_balance": true, "seeding": "RANDOM" } },
  "template": { "code": "FOOTBALL_LEAGUE", "version": 1 },
  "contest": {
    "period_seconds": { "FIRST_HALF": 2700, "HALF_TIME": 900, "SECOND_HALF": 2700 },
    "tie_resolution": "DRAW",
    "extra_period_sequence": [],
    "coverage_default": "STANDARD"
  },
  "lineup":        { "starters": 11, "bench_max": 9, "requires_approval": false, "positions": { "GK": { "min": 1, "max": 1 } } },
  "substitutions": { "max": 5, "windows": 3, "extra_in_extra_time": 1 },
  "roster":        { "min": 18, "max": 30, "requires_approval": true, "lock_policy": "FIRST_CONTEST" },
  "standings": {
    "points": { "win": 3, "draw": 1, "loss": 0, "no_result": 0, "forfeit_win": 3, "forfeit_loss": 0 },
    "forfeit_score": { "for": 3, "against": 0 },
    "tiebreakers": ["POINTS", "GOAL_DIFFERENCE", "GOALS_FOR", "H2H_POINTS", "H2H_DIFFERENCE", "H2H_SCORED", "DRAWING_OF_LOTS"],
    "h2h_reapply": true
  },
  "operations": {
    "readiness": ["LINEUPS_APPROVED", "OFFICIALS_MINIMUM", "VENUE_SET"],
    "officials_minimum": { "REFEREE": 1 },
    "scorer_correction_window_minutes": 60,
    "starting_soon_minutes": 30
  },
  "eligibility": { "membership_required": true, "min_age": null, "max_age": null, "age_reference_date": null, "minor_consent_required": "TENANT_DEFAULT" }
}
```

Operational code always reads the snapshot of the edition (AR-14). Stage-specific overrides
(e.g. sevens finals with longer halves, knockout `tie_resolution`) live under `stages.<STAGE_CODE>` in the
snapshot and override the defaults for contests in that stage.

**As built (S05-T4b).** `IRulesetResolver` (Contracts' `Rulesets`) takes a template (its latest version unless given),
one of the formats it lists (latest unless given), the format's parameters and the changes keyed by the template's
editable paths, and answers a snapshot or every problem with its field (one per field): `format.code`,
`format.version`, `format.parameters.<code>`, `overrides.<path>`, and `template` or `template.version` for a template
that doesn't exist. It checks in this order: template, format (listed, takes the sport's contest shape), format
parameters (`FORMAT_PARAMETER_INVALID`), each change (editable, type, bounds, allowed codes:
`RULESET_PARAMETER_UNKNOWN`, `RULESET_PARAMETER_OUT_OF_RANGE`; tiebreakers and tie resolutions the sport can't use
answer `RULESET_UNKNOWN_TIEBREAKER` and `RULESET_TIE_RESOLUTION_UNSUPPORTED`), the merged values against the template
schema, then the rules between values above, each problem on the change that causes it. A value of the wrong type is
out of range. It reads the template's pinned sport version from the stored definitions. The snapshot is written as
above (snake_case keys, every format parameter with its default filled in), canonicalised by RFC 8785; its hash is
`sha256:` and the hex SHA-256 of that text, so the same inputs give the same hash, whatever their order and whether a
default is stated or left out. Nothing is stored in S05: an edition stores the canonical JSON and its hash (S09).

`POST /api/admin/v1/ruleset-templates/{code}/preview?version=` (`ruleset.read`, not audited, a read with a body) takes
`{ "format": { "code", "version"?, "parameters"? }, "overrides"? }` and answers the snapshot in the API's names
(camelCase records, the same content) with its `hash`, or `422` with the first problem's code (by field) and one
`errors` entry per field. A value that isn't a whole number, true or false, a code or a list of codes (`null`, `3.5`,
an object) is refused on its own field like a value of the wrong type, or as `RULESET_PARAMETER_UNKNOWN` when the
template has no such editable path. An unknown template or `version` is
`404 RULESET_TEMPLATE_NOT_FOUND`; a missing format or format code, a `format.version` below 1 or another member is
`400 VALIDATION_INVALID_REQUEST` on its field. There is no platform preview (S05 D16).

## Tiebreaker catalogue (Standings & Progression)

Standings rows per stage/group: played, won, drawn, lost, no result, scored, conceded, difference, points
(with bonus breakdown), form (last 5), position. Criteria are applied in the order the snapshot lists.

| Code | Definition |
|---|---|
| `POINTS` | Total league points incl. bonus points |
| `WINS` | Number of wins |
| `GOAL_DIFFERENCE` / `DIFFERENCE` | Scored − conceded (goals/points — same computation, sport label differs) |
| `GOALS_FOR` / `SCORED` | Total scored |
| `TRIES_SCORED`, `TRY_DIFFERENCE` | Rugby: from timeline `TRY` + `PENALTY_TRY` events of approved contests |
| `NET_RUN_RATE` | Cricket, see below |
| `H2H_POINTS`, `H2H_DIFFERENCE`, `H2H_SCORED` | Same measures computed **only from contests among the tied participants** (mini-table) |
| `FAIR_PLAY` | Later: card points (yellow 1, second yellow 3, red 3 … configurable) — lower is better |
| `DRAWING_OF_LOTS` | Not computable: the participants remain tied and the standings show `PROGRESSION_TIE_UNRESOLVED` until an authorised user records the draw outcome as an override (reason + audit) |

**Head-to-head semantics:** when two or more participants are tied on all earlier criteria, build a mini-table of
the contests among exactly those participants and apply the `H2H_*` criteria. If `h2h_reapply = true` and a
subset is still tied after the H2H criteria, re-apply the H2H criteria to that subset only, then continue with
the remaining non-H2H criteria for anyone still tied.

**Net run rate (NRR):** `NRR = (runs scored ÷ overs faced) − (runs conceded ÷ overs bowled)` across the stage,
where overs = legal balls ÷ 6 as an exact fraction (75 balls = 12.5 overs, written "12.3"). If a side is bowled
out, its overs faced count as the full quota for that innings (e.g. 20 overs in T20). Matches with revised
targets use the revised overs; no-result matches and super overs are excluded. Display to three decimals.

**Determinism:** recalculation from the same approved results must produce the same rows and positions (hash
compared in tests).

## v1 templates (defaults — confirm with each customer's competition regulations)

| Template | Key defaults |
|---|---|
| `FOOTBALL_LEAGUE` | 2×45 min; draw allowed; 3/1/0; 11 starters, bench 9, 5 subs in 3 windows; tiebreakers as in the example |
| `FOOTBALL_CUP` | as league, plus `tie_resolution: EXTRA_PERIODS_THEN_SHOOTOUT` (2×15 min extra time; one extra substitution in extra time) |
| `RUGBY15_LEAGUE` | 2×40 min; 4 win / 2 draw / 0 loss; bonus +1 for ≥4 tries (`try_bonus: ABSOLUTE 4`), +1 for losing by ≤7 (`losing_bonus_margin: 7`); 15 starters + 8 replacements; sin bin 10 min; tiebreakers `POINTS, H2H_POINTS, DIFFERENCE, TRY_DIFFERENCE, SCORED, TRIES_SCORED, DRAWING_OF_LOTS` |
| `RUGBY7_TOURNAMENT` | 2×7 min halves, 2-min half-time; pool points 3 win / 2 draw / 1 loss; knockout `EXTRA_PERIODS_SUDDEN_DEATH` (5-min periods); 7 starters + 5 replacements; sin bin 2 min; finals duration as a stage override |
| `BASKETBALL_LEAGUE` | 4×10 min quarters (FIBA); 5-min overtimes until decided; win 2 / loss 1 / forfeit 0; 5 starters, up to 12 dressed; tiebreakers `POINTS, H2H_POINTS, H2H_DIFFERENCE, H2H_SCORED, DIFFERENCE, SCORED` |
| `BASKETBALL_PLAYOFFS` | knockout with `tie_type: SERIES(best_of 3/5/7)`; overtime until decided |
| `CRICKET_T20_LEAGUE` | 20 overs per side; max 4 overs per bowler; free hit on; `tie_resolution: SUPER_OVER` with `super_over_repeat: true` (some leagues use `TIE`); 2 win / 1 tie or no result / 0 loss; tiebreakers `POINTS, WINS, NET_RUN_RATE, H2H_POINTS, DRAWING_OF_LOTS` |

Rugby bonus variants are parameters, not code: `try_bonus` may be `ABSOLUTE n` (n or more tries) or
`RELATIVE n` (n more tries than the opponent); `losing_bonus_margin` is configurable (some competitions use 5).

**These defaults reflect widely used international rules; local competitions differ. Every template value must
be reviewed against the customer's regulations before a real competition uses it** (docs/build/open-questions.md
Q9).
