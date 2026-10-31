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
`config.sport_definitions` (one row per variant and version) with its content (`jsonb`, written from its RFC 8785
canonical JSON) and the hash `sha256:<hex>` of that canonical text (`CanonicalJson`; `jsonb` doesn't keep the text, so
re-hash by canonicalizing again). The hash covers content, not the file: comments and layout don't
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

Effective status (derived): `REVERSED` if a later `ACTIVE` event reverses it; else `SUPERSEDED` if a later event
that is not `REVERSED` supersedes it; else `ACTIVE` (owner, 2026-10-30: only an event that still stands counts, so
reversing a reversal or a correction restores what it had undone, and a chain of corrections ends at its last one). A superseding event is itself a full replacement event (e.g. same `GOAL` with the
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

**As built (S05-T2a).** The contract and its input types are in Sports Configuration Contracts (`Engines`), named as
in ADR-0003, until Match Operations maps its timeline rows to them (S12): `TimelineEventDraft`, `TimelineEvent`
(id, sequence, type, source, participant, period code and occurrence, clock and added time, actors by role, payload
as text, `supersedes`/`reverses` by event id), `ContestContext` (definition, `ContestRules`, sides, coverage,
lineups), `ContestLiveState` (stored `ContestStatus`, score, `ContestPhase`, `ContestPersonnel`, every event with its
effective status). `ContestRules` holds the effective rules flattened to dotted paths (`substitutions.max`,
`contest.period_seconds.FIRST_HALF`), the names the definitions' `limits` and the templates' editable parameters
use. `IScoringEngines` picks the engine by `scoring_family` and folds a timeline. The fold and the trackers live next
to the engines in the module's `Engines` folder. An architecture test (`EngineDependencyTests`) lets that folder use,
of the application, only BuildingBlocks' results, the module's Contracts and its errors, and refuses the base
library's files, resources, network, environment, reflection, threads, clock and random sources (a type-level rule
can't see `Guid.NewGuid()`; review does). As built:
- **Effective status:** only a later event that still stands counts. An event is `REVERSED` by a later *active*
  event that reverses it, else `SUPERSEDED` by a later event that supersedes it and is not itself reversed (so a
  chain of corrections ends at its last one). Reversing a reversal or a correction restores what it had undone.
- **Phase:** a `PERIOD_START` names any period, breaks included, in the occurrence recorded on it (the stored
  `period_occurrence`, which the score's periods read too, so reversing an earlier start renumbers nothing); a
  `PERIOD_END` closes the running one, and until the next start the phase kind is `BREAK`.
- **Personnel:** roles are read from the definition, never by name. An effect applies to the event's first
  declared actor role that is filled; a substitution takes off the player in the role drawn `ON_FIELD` and brings
  on the one drawn from the `BENCH`. A suspension doesn't touch someone already replaced, dismissed or temporarily
  replaced. `RETURN` brings a suspended person back where they were (field, bench or staff),
  or a temporarily replaced player back on (named either way), whose replacement goes back to the bench if still
  on the field. `FOUL_COUNT` counts per person.
- **ADDITIVE state:** per participant, points per component and `main` points per period, plus every period
  started; the summary shows `main` as primary text, the `shootout` component as secondary text once a `SHOOTOUT`
  period has started, and one entry per `PLAY` period started (its `short` label).

**As built (S05-T2b1).** `TimelineChecks` (next to the trackers) runs the checks every family shares, in groups; the
first group that fails answers (all 422):
- **Type:** the definition's or a generic one (`TIMELINE_EVENT_UNKNOWN`).
- **Status:** `LIVE`; `ENDED` while the caller says the correction window is open
  (`ContestLiveState.CorrectionWindowOpen`, set on the folded state by Match Operations, which has the clock; the fold
  leaves it false), for corrections and reversals only, and never a reversal of a `PERIOD_START` or `PERIOD_END` (§9;
  a new period or goal, or a period running again, would reopen the contest, which is `ENDED → LIVE`); any status for
  `BACKFILL` and `ADMIN` (`TIMELINE_CONTEST_NOT_LIVE`).
- **References:** a reversal names an active event and is `EVENT_REVERSED` or a type in that event's `reversible_by`;
  `EVENT_REVERSED` needs one; a correction replaces an active event of the definition with another (generic events
  are reversed, never corrected), of any of the definition's types; never both (`TIMELINE_REFERENCE_INVALID`). A
  correction is checked **as at the event it replaces** (owner, 2026-10-30): phase and people are folded from the
  active events before the first event of its chain of corrections, each standing where its own chain started (an
  earlier event corrected later counts in its corrected form), and Match Operations records it in that event's
  period and occurrence. A reversal is checked in the current phase: a type that lists only `PLAY` (football's
  `VAR_DECISION`) can't reverse at half-time or after the end, `EVENT_REVERSED` can.
- **Phase:** a definition's type must list the current phase kind (the running period's, or `BREAK` between
  periods; nothing before the first start); `PERIOD_START` needs a period of the definition and none running;
  `PERIOD_END` needs one running; other generic events come any time (`TIMELINE_EVENT_NOT_ALLOWED_IN_PHASE`).
- **Participant:** a type that scores, names people, has effects, limits or a follow-up needs a participant; any
  participant named must be the contest's (`TIMELINE_PARTICIPANT_INVALID`).
- **Actors:** a role is required from its `required_from` coverage, or always when neither optional nor given one
  (`TIMELINE_ACTOR_REQUIRED`); a role the type doesn't declare, or one equal to its `distinct_from`, is
  `TIMELINE_ACTOR_INVALID`. From `STANDARD`, each person must be in the role's pool on the role's side: `ON_FIELD`
  (else `TIMELINE_ACTOR_NOT_ON_FIELD`), `BENCH`, `STAFF`, `SQUAD` (field, bench, suspended, temporarily replaced or
  replaced), `SQUAD_OR_STAFF` (else `TIMELINE_ACTOR_INVALID`); a dismissed person is in no pool.

**As built (S05-T2b2).** `FollowUpsAndLimits` is the last group, run once the actors are valid; both errors are
reported when both fail (422). Both read the active events each where its chain of corrections started, with the
draft in its place: a correction where the event it replaces stood, a new event at the end.
- **Follow-up:** an event needs an earlier active event of a type in `requires_preceding`; with `same_side` the
  same participant's, with `same_period` in the same period and occurrence, with `unconsumed` one no event of a
  `consumes` type has used yet. An event that consumes takes the latest earlier event its follow-up accepts. The
  whole timeline is walked: the draft must find its earlier event, and every follow-up that had one must keep the
  same one (`TIMELINE_FOLLOW_UP_MISSING`). So a try corrected after its conversion is still converted, but a
  correction can't give a try a second conversion, move a converted try to the other team or another type, or move
  a conversion onto another try: the scorer reverses the conversion first (review #80, owner 2026-10-30). Reversing a try doesn't take back the
  conversion that used it (S12 or S15 decides); such a conversion blocks nothing else.
- **Limits:** each `{ rule }` is read from `ContestRules`; a rule the rules don't set is no limit. A new event's
  limits are checked for its participant; a correction's, and the replaced event's, for every participant (changing
  a substitution into a card can split a window, the other team's too: its substitutions keep a run open; review
  #80). The kind is the rule's last segment: `…max` counts the
  participant's events of every type with that limit and adds `<group>.extra_in_extra_time` once an overtime `PLAY`
  period has started (so not yet in `AWAITING_EXTRA_TIME`, a plain break; Q33), for corrections of earlier events
  too (owner, 2026-10-30); `…windows` counts the participant's runs of such events in `PLAY`. A run ends at a
  period's start or end, at another event of the definition (any participant's events with the limit don't end it),
  or when the participant's next one is at another game-clock minute than the last one with a clock (owner,
  2026-10-30; an event without a clock doesn't end a run, nor start a new one by its minute); events in a break use
  no window.
  `extra_in_extra_time` adds no window (owner, 2026-10-30). A reversed event frees its place
  (`TIMELINE_LIMIT_REACHED`). Any other kind (basketball's `timeouts.per_period_group`) throws until its sport's
  slice.
- **Points override:** Apply uses `scoring.points.<EVENT_TYPE>` from the rules for a type whose definition gives it
  points other than 0; a type worth 0 (a missed attempt) or none never scores. Whether a template lets tenants change
  it is the resolver's check (T4).
- **Purity:** besides the type-level rule, `EngineDependencyTests` reads the source of both `Engines` folders
  (module and Contracts) for calls that make ids or read the clock (`Guid.NewGuid`, `DateTime.UtcNow`, `Random.`,
  `Environment.`, …).
- **Scenarios:** each file names the rules it shows (`covers`). `ScenarioRuleCoverageTests` fails when a rule of
  `ScenarioRules` has no covering scenario that accepts an event of the sport (any of the definition's types, not
  necessarily one the rule governs) or, unless it can't refuse anything (Apply), none that refuses an event with one
  of the rule's error codes. Rugby 15s scenarios read the reference file in docs/domain/sports until S15 seeds
  rugby. A scenario may leave out `expect.outcome` until tie resolution (T2c).

**As built (S05-T2c).** `AdditiveEngine.ProposeOutcome` answers as the score stands; when the scorer may end the
contest is Contest Operations' call (S12; owner, 2026-10-30). `OutcomeProposal` has a `Kind` (`Winner`, `Draw`,
`RequiresExtraTime`, `RequiresShootout`), the winner and `Method` (`Regulation`, `ExtraTime`, `Overtime`,
`SuddenDeath`, `Shootout`), `NextPeriodCode` and `RequiresEnd`. The rules are `contest.tie_resolution` (unset is
`DRAW`) and `contest.extra_period_sequence`; the state also knows whether the last period started is still running.
- **Different `main` totals** win. A score in a started `sudden_death` period is `SuddenDeath` with `RequiresEnd`;
  otherwise, once an overtime `PLAY` period has started, `Overtime` under `REPEAT_EXTRA_PERIOD` and `ExtraTime` under
  the others (the tie resolution names the method, not the sport; owner, 2026-10-30); else `Regulation`.
- **Level totals:** a draw under `DRAW`. Once a `SHOOTOUT` period has started, the `shootout` components decide
  (`Shootout`), and while they are level the proposal is `RequiresShootout` with no next period. Otherwise the next
  period is the one after the last started entry of the sequence (the first if none has); its kind, or for a break
  the next non-break entry's, gives `RequiresExtraTime` (`PLAY`) or `RequiresShootout`, with that period as
  `NextPeriodCode` (level at the end of normal time in a cup: `AWAITING_EXTRA_TIME`). Past the end of the sequence, a
  last `PLAY` period still running is `RequiresExtraTime` with no next period; once it has ended the sequence starts
  again at its first `repeatable` entry (basketball's `AWAITING_OVERTIME`), or nothing is left and level is a draw.
- **Sudden death in Validate:** with the phase group, a scoring event (points other than 0 in `main`, after the
  rules' override, as Apply counts them) in a `sudden_death` period is `TIMELINE_EVENT_NOT_ALLOWED_IN_PHASE` while
  another one stands in the same occurrence (owner, 2026-10-30); correcting or reversing the deciding score stays
  possible, and a miss scores nothing. The outcome reads a sudden-death win from any `main` points in such a period
  and gives the winner by the totals; the two differ only if an earlier period is corrected after sudden death
  started, and Results approves either way.
- **Not checked:** which period may start next (a league starting extra time, extra time with one side ahead) is
  Contest Operations' offer from the proposal (S12).
- **Scenarios:** every file states `expect.outcome` (`winner` and `method`, with `requires_end`; `draw: true`; or
  `requires: EXTRA_TIME | SHOOTOUT` with `next_period`), and an event may state `outcome` as it stands after it. The
  outcome rules are in `ScenarioRules`: `OUTCOME_WINNER`, `OUTCOME_DRAW`, `OUTCOME_EXTRA_PERIODS`, `OUTCOME_SHOOTOUT`
  (they refuse nothing) and `OUTCOME_SUDDEN_DEATH` (refuses the second score). Sudden death is shown with rugby 15s'
  reference file and a test rules file `rules/RUGBY15_KNOCKOUT.yaml`; repeated overtime with basketball's, in unit
  tests (`AdditiveOutcomeTests`).

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
