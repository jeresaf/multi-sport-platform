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
