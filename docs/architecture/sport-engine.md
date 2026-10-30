# Sport engine — definitions, timeline, scoring families

Decisions: ADR-0002 (timeline is truth), ADR-0003 (definitions + engines), ADR-0006 (configuration in repo).
Owners: **Sports Configuration** (definitions, engines, generators) and **Match Operations** (timeline, contest
state). Sport-definition reference files: docs/domain/sports/.

## 1. Concepts

| Term | Meaning | Example |
|---|---|---|
| Sport | Broad discipline | Rugby |
| Sport variant | Distinct playable form; the unit we define | `RUGBY_UNION_7` |
| Definition version | Immutable, hashed content of a variant definition | `RUGBY_UNION_7@1` (sha256 …) |
| Scoring family | Engine that computes score/outcome | `ADDITIVE`, `INNINGS`, `SETS`, `RANKED_METRIC`, `COMBAT` |
| Contest shape | `HEAD_TO_HEAD` (two sides) or `FIELD` (many ranked participants) | Football = H2H, Golf round = FIELD |
| Coverage level | How much detail a contest records | `RESULT_ONLY`, `BASIC`, `STANDARD`, `DETAILED` |
| Effective rules | Edition's frozen snapshot of rule values (docs/domain/rulesets.md) | `points_win: 3`, `period_duration_seconds.FIRST_HALF: 2700` |

**Coverage levels** let small tenants score quickly and big tenants record everything, and stop the UI from
implying data that wasn't captured (AR-30):
- `RESULT_ONLY` — final score only (typical for backfill).
- `BASIC` — score-changing events with optional actors (no lineups required).
- `STANDARD` — lineups required; scorers/assists, cards, substitutions with actors.
- `DETAILED` — plus detailed events (shots, corners, fouls, rebounds …).

The edition's rules set a default coverage; a contest may be downgraded (e.g. scorer unavailable), never silently
upgraded after the fact.

## 2. Definition content (summary)

Full schema and annotated examples: docs/domain/sports/README.md. A definition declares:
terminology · shape · participant types · scoring family · periods · event types (category, importance, points,
side, actor roles, metadata schema, follow-up constraints, effects, coverage level) · positions · staff roles ·
official roles · statistics (with derivation) · lineup defaults · scorer pad layout · rule parameters the
family understands, with defaults.

**As built (S05-T1b1).** The seeder `config.configuration.sports` stores each embedded file in
`config.sport_definitions` (one row per variant and version) with its content as RFC 8785 canonical JSON and the hash
`sha256:<hex>` of that text (`CanonicalJson`). The hash covers content, not the file: comments and layout don't
change it. A stored `(variant, version)` whose file now hashes differently fails the seed with
`SPORT_DEFINITION_IMMUTABLE`; a unit test pins football@1's hash. `ISportDefinitions` (Contracts) reads the rows once
into typed records (`SportDefinition`; the generic event types in `GenericEventTypes`, separate from `EventTypes`
because README.md gives them no importance, coverage or phases) and keeps them until restart.

## 3. Timeline event (Match Operations)

| Field | Notes |
|---|---|
| `id` | UUIDv7 |
| `tenant_id`, `contest_id` | |
| `sequence` | Server-assigned, gapless per contest; authoritative order |
| `client_event_id` | Unique per contest; idempotency key from the scorer device |
| `device_id`, `source` | `LIVE`, `OFFLINE_SYNC`, `BACKFILL`, `PROVIDER`, `ADMIN` |
| `event_type`, `category`, `importance` | From the definition (category/importance copied for fast filtering) |
| `period_code`, `period_occurrence` | e.g. `OVERTIME`, occurrence 2 |
| `game_clock_seconds`, `added_time_seconds` | Clock at the moment (football 45+2 = 2700 + 120) |
| `participant_id` | Contest participant the event belongs to (acting side) |
| actors | Separate table `timeline_event_actors(event_id, role, athlete_id)` for statistics queries |
| `payload` | JSONB validated against the event type's metadata schema |
| `supersedes_event_id` / `reverses_event_id` + `reason` | Corrections / reversals (never edits) |
| `recorded_by`, `recorded_at`, `occurred_at` | Server time vs. device-reported time |
| `timeline_version` | Contest timeline counter after this event |

Effective status (derived): `REVERSED` if any later event reverses it; else `SUPERSEDED` if any later event
supersedes it; else `ACTIVE`. A superseding event is itself a full replacement event (e.g. same `GOAL` with the
correct scorer).

## 4. Engine contract

```csharp
public interface IScoringEngine
{
    string Family { get; }
    ScoreState Initial(ContestContext ctx);
    ValidationResult Validate(ContestContext ctx, ContestLiveState live, TimelineEventDraft draft);
    ScoreState Apply(ContestContext ctx, ScoreState state, TimelineEvent evt);
    OutcomeProposal ProposeOutcome(ContestContext ctx, ScoreState state);
    ScoreSummary Summarize(ContestContext ctx, ScoreState state);   // neutral display model for every client
}
```

- `ContestContext` — resolved definition version, effective rules snapshot, contest participants (sides),
  coverage level.
- `ContestLiveState` — current score state + status + phase/clock + personnel state (who is on the field).
- `Fold` (generic, not per family): `activeEvents.OrderBy(sequence).Aggregate(Initial(ctx), Apply)`.
- Engines are **pure**: no I/O, no clock, no randomness. All inputs are in the arguments.
- `ScoreSummary` is a structured, family-neutral object (per participant: primary text, secondary text,
  period breakdown, status badges) so web and mobile render scores without sport logic.

### Generic helpers shared by all families

- **Phase tracker** — current period from `PERIOD_START`/`PERIOD_END` events and the definition's period list
  (kinds `PLAY`, `BREAK`, `SHOOTOUT`).
- **Personnel tracker** — who is on the field per side, from the approved lineup plus events with effects:
  `SUBSTITUTE_OUT_IN`, `SEND_OFF`, `TEMPORARY_SUSPENSION(seconds)`, `RETURN`, `DISQUALIFY`. Used for actor
  validation and minutes played.
- **Follow-up constraints** — declared in data, e.g. a conversion requires a preceding, unconsumed try by the
  same side in the same period.
- **Limits** — declared rule parameters such as maximum substitutions or timeouts per period group.

## 5. `ADDITIVE` family (football, rugby 15s/7s, basketball; later netball, hockey, handball)

**State:** per side: `total`, `by_period` (period code + occurrence → points), and named components
(`shootout`).

**Apply:** for an active event whose type has `points: n` and `side: OWN | OPPONENT`, add `n` (or the rules
override for that event type, if the template allows overrides) to the acting side or its opponent, in the
component the type names (default `main`). Non-scoring events don't change score.

**Validate (in addition to generic checks):**
1. Contest is `LIVE` (or `ENDED` within the correction window, or source `BACKFILL`/`ADMIN`).
2. Event type allowed in the current phase kind (scoring in `PLAY`; shootout types only in `SHOOTOUT`).
3. Required actors present; with coverage ≥ `STANDARD`, actors must be on the field (personnel tracker),
   except roles explicitly allowed off-field (e.g. staff for cards).
4. Follow-up constraints and limits satisfied.

**Outcome:**
- Different `main` totals → winner, method `REGULATION` or `EXTRA_TIME`/`OVERTIME` if decided there.
- Level totals → apply `tie_resolution` from rules (allowed values are listed in the definition's
  `supports.tie_resolutions`):
  - `DRAW` (leagues);
  - `EXTRA_PERIODS_THEN_SHOOTOUT` (football cups; rugby 15s knockouts with a kicking competition) → engine returns
    `REQUIRES_EXTRA_TIME`, then `REQUIRES_SHOOTOUT`; winner by `shootout` component, method `SHOOTOUT`;
  - `REPEAT_EXTRA_PERIOD` (basketball overtime until decided);
  - `EXTRA_PERIODS_SUDDEN_DEATH` (rugby sevens knockouts: first score in extra time wins, method `SUDDEN_DEATH`);
  - `SHOOTOUT` directly (some cups).
- Which extra periods are played, and in what order, comes from the rule `contest.extra_period_sequence`
  (period codes from the definition). In a period flagged `sudden_death: true`, the first scoring event ends the
  contest (`REQUIRES_END`).
- Contest Operations reads `REQUIRES_*` to offer the scorer the next period instead of "End match".
- Methods outside the engine (set by Results): `WALKOVER`, `FORFEIT`, `AWARDED`, `ABANDONED_VOID`.

Two-legged ties (aggregate scores) are a format/progression concern and are out of scope for v1
(docs/domain/formats.md).

## 6. `INNINGS` family (cricket T20 first)

**Events:** `TOSS` (winner, decision), `INNINGS_START` (batting side, openers), `DELIVERY`, `BATTER_RETIRED`
(`RETIRED_HURT` = not out; `RETIRED_OUT` = counts as a wicket), `BATTER_TIMED_OUT` (counts as a wicket),
`INNINGS_END` (reason), `REVISED_TARGET` (manual DLS entry: runs + overs), `INTERRUPTION`/`RESUMPTION`,
`SUPER_OVER_START`. A delivery is **one event** — not separate RUN/FOUR/WIDE events.

**`DELIVERY` payload:**

```json
{
  "bowler_id": "…", "striker_id": "…", "non_striker_id": "…",
  "runs_off_bat": 4,
  "boundary": "FOUR",
  "extras": { "kind": "NONE", "runs": 0 },
  "penalty_runs": null,
  "wicket": null
}
```

- `extras.kind`: `NONE | WIDE | NO_BALL | BYE | LEG_BYE`; `extras.runs` is the total extra runs for the delivery
  **including** the automatic one run for a wide or no-ball.
- `wicket`: `{ "kind": BOWLED|CAUGHT|LBW|RUN_OUT|STUMPED|HIT_WICKET|HIT_BALL_TWICE|OBSTRUCTING_THE_FIELD, "player_out_id", "fielder_id"? }`
  (`player_out_id` may be the non-striker for run outs). Bowled, caught, LBW, stumped and hit wicket are
  credited to the bowler; the others are not.
- `penalty_runs`: `{ "to": "BATTING" | "FIELDING", "runs": 5 }`.

**State per innings:** `batting_participant_id`, `runs`, `wickets`, `legal_balls`, `extras {wides, no_balls,
byes, leg_byes, penalties}`, `max_balls`, `target?`, `closed`, `close_reason`. Engine `Detail` also carries
batting and bowling cards for live scorecards.

**Rules enforced:**
- Legal ball unless `WIDE` or `NO_BALL`. Over = 6 legal balls. A bowler may not bowl consecutive overs;
  maximum overs per bowler from rules (T20 default 4).
- On a wide: `runs_off_bat` must be 0; dismissals allowed only `STUMPED`, `RUN_OUT`, `HIT_WICKET`,
  `OBSTRUCTING_THE_FIELD`.
- On a no-ball: dismissals allowed only `RUN_OUT`, `HIT_BALL_TWICE`, `OBSTRUCTING_THE_FIELD`; the next
  delivery is a free hit when `free_hit_enabled` (T20 default true), on which only those same dismissals apply.
- Innings closes at `wickets == players_per_side − 1`, at `max_balls`, when the target is reached (second
  innings), or by `INNINGS_END` with a reason.
- Target = first-innings runs + 1, unless a `REVISED_TARGET` event sets runs/overs.

**Outcome:**
- Chasing side reaches target → wins by `(players_per_side − 1 − wickets)` wickets.
- Otherwise defending side wins by `(target − 1 − chasing runs)` runs.
- Equal → if `tie_resolution` is `SUPER_OVER`, engine returns `REQUIRES_SUPER_OVER` (1 over, max 2 wickets per
  side; repeated while tied if `super_over_repeat`); if it is `TIE`, the result is a tie.
- Abandoned without a result → `NO_RESULT` (set by Results with the contest status).

**Display:** overs are `legal_balls / 6` written as `overs.balls` (e.g. 75 balls = `12.3`). Never treat `12.3`
as a decimal in calculations — net run rate uses exact balls (docs/domain/rulesets.md).

## 7. Later families (sketches — specify fully before their slice)

- **`SETS`** (volleyball, tennis, badminton): points → games (tennis) → sets; `points_to_win_set`, `win_by`,
  `cap`, `deciding_set_points`, `sets_to_win`, tiebreak rules; serve tracking optional.
- **`RANKED_METRIC`** (golf, athletics, swimming): per-participant metric per round/attempt; direction
  (`ASC` lower is better / `DESC`); aggregation (sum of rounds, best attempt); ties and count-backs; cut lines;
  wind/legality flags for athletics.
- **`COMBAT`** (boxing, MMA): rounds; judges' scorecards as events (`JUDGE_SCORECARD` per round/judge);
  outcome methods `KO`, `TKO`, `SUBMISSION`, `DECISION_UNANIMOUS|SPLIT|MAJORITY`, `DQ`, `NO_CONTEST`, `DRAW`;
  round and time of finish.

## 8. Phases and clock

- Periods come from the definition: `code`, `label`, `kind` (`PLAY|BREAK|SHOOTOUT`), `duration_seconds`
  (default; rules may override per period and per stage), `clock` (`COUNT_UP|COUNT_DOWN|NONE`),
  `clock_starts_at_seconds` (football second half shows 45:00), `overtime`, `repeatable`.
- The contest stores `current_period_code`, `current_period_occurrence`, `phase_kind` and a clock state
  `{ running, period_started_at, accumulated_seconds }`. Clients compute the displayed clock from this plus
  server time offset; the server is authoritative.
- Clock events: `PERIOD_START`, `PERIOD_END`, `CLOCK_STOP`, `CLOCK_START`, `CLOCK_SET` (adjust with reason).
- The UI never hard-codes 45/40/10-minute periods (AR-29).

## 9. Corrections, reversals, offline

- **Correction** (wrong scorer, wrong minute): a new event with `supersedes_event_id`; the fold uses the new one.
- **Reversal** (VAR/TMO overturn, entered in error): a new event (`VAR_DECISION`, `TMO_DECISION` or the generic
  `EVENT_REVERSED`) with `reverses_event_id` and a reason; the original stays visible as reversed.
- Corrections after the contest has `ENDED` are allowed within a configurable window for scorers, and later
  only through the result-correction workflow (results officer, reason required).
- **Idempotency:** the same `client_event_id` twice returns the original event (no duplicate). A different
  payload with a reused `client_event_id` is rejected (`TIMELINE_IDEMPOTENCY_CONFLICT`).
- **Single active scorer:** one scorer device holds a renewable lease per contest; other devices are read-only
  unless they take over (audited). This removes most offline conflicts by design.
- Offline events (Phase 8) are appended in device order; if validation fails on sync (e.g. contest already
  ended), the event is `REJECTED` back to the device with the error code for the scorer to resolve. No
  last-write-wins, because nothing is overwritten.

## 10. UI metadata

- **Renderers** chosen by `(shape, scoring_family)`: H2H×ADDITIVE card (football/rugby/basketball),
  H2H×INNINGS card (cricket), H2H×SETS, H2H×COMBAT, FIELD×RANKED_METRIC. They render `ScoreSummary` + timeline,
  never compute scores.
- **Scorer pads**: ADDITIVE uses a generic button pad built from the definition's `scorer_pad`; INNINGS uses a
  delivery pad (runs 0–6, extras, wicket, next ball) — chosen by family, labelled by definition data.
- **Terminology** (`Match`/`Game`/`Bout`, `Half`/`Quarter`/`Innings`) comes from the definition.

## 11. Testing engines

- Scenario files per sport in `backend/tests/SportsPlatform.UnitTests/Engines/Scenarios/<variant>/*.yaml`:
  definition version, rule overrides, ordered events, expected score summary and outcome.
- Every rule in sections 5–6 has at least one passing and one rejecting scenario.
- Scenarios are readable by non-developers (owner, federation experts) — use realistic Ugandan fictional names.
