# Competition formats and fixture generators

Owner: Sports Configuration (definitions + generators), Competitions (instantiated structure), Match
Operations (contests and dates), Standings & Progression (who advances). Decisions: ADR-0003, ADR-0006.

A **format** answers "how is the competition organised?" — never "how does the sport score?". Any format can
pair with any sport whose contest shape it supports (`HEAD_TO_HEAD` or `FIELD`).

## Format definition (repo file, versioned like sport definitions)

```yaml
code: ROUND_ROBIN
version: 1
name: League (round robin)
compatible_shapes: [HEAD_TO_HEAD]
stages:
  - code: LEAGUE
    type: LEAGUE
    generator: ROUND_ROBIN
parameters:
  cycles:            { type: int,  min: 1, max: 4, default: 1 }      # 2 = home and away
  home_away_balance: { type: bool, default: true }
  seeding:           { type: enum, values: [SEEDED, RANDOM, MANUAL], default: RANDOM }
```

Stage types: `LEAGUE`, `GROUP`, `KNOCKOUT`, `SERIES`, `SINGLE`, and later `PLACEMENT`, `LEADERBOARD`, `HEATS`,
`CARD`.

## Generator rules (all generators)

- **Pure and deterministic:** output = f(ordered participants, parameters, random seed). Inputs, seed and output
  hash are stored (draw record / fixture generation run) so any draw can be reproduced and audited.
- Generators produce **structure and pairings only**. Dates, times and venues are assigned afterwards in Match
  Operations (bulk by round: start date, day-of-week pattern, kick-off time, default venue = home team's home
  venue), with conflict detection.
- A participant count the format can't handle returns a clear error (`FORMAT_PARTICIPANT_COUNT_INVALID`) — never
  a silently odd structure.

## v1 formats

### `SINGLE_CONTEST`
One stage, one contest. Used for friendlies, exhibitions, one-off finals and the "Quick match" flow (which
creates a lightweight competition + edition behind the scenes so every contest still belongs to an edition).

### `ROUND_ROBIN` (league)
- **Circle method.** If `n` is odd, add a BYE to make it even. Fix the first participant, rotate the rest;
  `n − 1` rounds per cycle, `n/2` pairings per round.
- **Home/away:** alternate the fixed participant's venue each round and orient other pairings to minimise
  consecutive home/away "breaks". For `cycles = 2`, the second cycle repeats the first with home/away swapped.
- Output: rounds `1..(n−1)×cycles` with pairings `(home, away)`; BYE pairings produce no contest but are shown
  in the round list.
- Tests: every pair meets exactly `cycles` times; each participant plays once per round (except BYE); home and
  away counts differ by at most 1 per cycle; deterministic for the same seed.

### `KNOCKOUT` (single elimination)
- Bracket size = next power of two ≥ `n`; byes = size − `n`, given to the top seeds.
- Standard seed placement so seeds 1 and 2 can meet only in the final, 1–4 only in semi-finals, etc.
- Seeding: `SEEDED` (given order), `RANDOM` (stored seed), `MANUAL` (admin places participants in slots).
- Later rounds contain **slots** (`Winner of QF1`) resolved by Standings & Progression after the feeding
  result is approved; Match Operations fills the participants on `SlotResolved`.
- Parameters: `third_place_contest` (bool), `tie_type` (`SINGLE_CONTEST` or `SERIES` with `best_of`).
- Round names derive from remaining participants: Final, Semi-finals, Quarter-finals, Round of 16, Round of N.
- Two-legged ties (aggregate scores) are **not in v1**.

### `GROUPS_THEN_KNOCKOUT`
- Parameters: `group_count`, `cycles` (1 or 2), `qualifiers_per_group`, `best_placed_qualifiers`
  (`position` + `count`, only when all groups have equal size in v1), `allocation` (`SEEDED_SNAKE`, `RANDOM`,
  `MANUAL`), knockout parameters as above.
- Groups are balanced (sizes differ by at most one).
- Knockout pairing uses cross-over (A1 v B2, B1 v A2, C1 v D2, D1 v C2 …) and avoids same-group meetings in the
  first knockout round where possible.
- Standings per group come from Progression using the edition's tiebreakers.

### `SERIES` (best-of-N)
- Between two participants; `best_of` ∈ {3, 5, 7}; `home_pattern` (e.g. `[A, A, B, B, A, B, A]` for 2-2-1-1-1).
- Creates the first `ceil(best_of/2)` contests up front; further contests are created only when needed; the
  series ends when a side reaches `ceil(best_of/2)` wins (remaining scheduled contests are cancelled with reason
  `SERIES_DECIDED`).
- Used standalone or as the tie type inside `KNOCKOUT` (basketball playoffs).

## Later formats (specify before their slice)

| Format | Notes |
|---|---|
| `POOLS_WITH_PLACEMENT` | Rugby sevens: pools → Cup/Plate/Bowl/Shield brackets by pool position |
| `TWO_LEG_KNOCKOUT` | Aggregate score; away-goals rule off by default; extra time/shootout in second leg |
| `LEADERBOARD` | Multi-round field events (golf); cut after round k (top N and ties) |
| `HEATS_TO_FINAL` | Athletics: heats → semis → final; qualify by place (Q) and by fastest losers (q) |
| `CARD` | Fight card: ordered bouts in sections (main card, prelims); no progression |
| `SWISS`, `CONFERENCE_PLAYOFF` | When a customer needs them |

## Instantiation (Competitions)

When an edition's entrants are confirmed:
1. Instantiate stages, groups and rounds from the format + parameters (ADR-0006 snapshot).
2. Seed/draw participants (`stage.draw.run` stores inputs + seed + result; `stage.draw.override` needs a reason).
3. Publish the structure (`stage.publish`).
4. Match Operations generates contests for the published structure (`fixture.generate`), then dates/venues,
   then publishes fixtures.
