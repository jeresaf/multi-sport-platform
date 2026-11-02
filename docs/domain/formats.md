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

**As built (S05-T3a).** The files are `single-contest.yaml`, `round-robin.yaml` and `knockout.yaml`, embedded in
Sports Configuration under `Definitions/Formats` with `format-definition.schema.json`. Each file has `schema_version`
(1), `code`, `version`, `name`, an optional `description`, `compatible_shapes`, `participants: { min, max }` (both
ends included; `min` at least 2), `stages` (`code`, `type`, `generator`) and `parameters`, a map keyed by lower snake
case codes. A parameter has `type` (`int` with `min` and `max`, `bool`, or `enum` with `values`), `label`, an optional
`description` and a `default`. Rules in code (`FormatDefinitionRules`): unique stage codes; the generator is one of
`FormatGeneratorKinds` (Contracts) and fills the stage's type and pairs every compatible shape; a one-stage format
takes no more participants than its generator; the format declares exactly the parameters its generators read, with
their types and only choices they understand; bounds are in order and hold the default. The participant limits are
`SINGLE_CONTEST` 2–2, `ROUND_ROBIN` 2–40 and `KNOCKOUT` 2–256 (owner, S05-T3 plan; a new version can raise them). The schema caps `participants` at 1024 and
bounds codes (50 characters), names (100) and whole numbers (32-bit) as the table and the typed model store them; a
higher participant limit needs a schema change (review #82).
`KNOCKOUT@1` has no `tie_type`: `SERIES` comes with S16 in `KNOCKOUT@2`. Seeded, hashed and immutable like sport
definitions (`config.format_definitions`, `FORMAT_DEFINITION_IMMUTABLE`); read with `format.read`.

**As built (S05-T3b).** `IFormatGenerators.Generate(format, input)` (Contracts, `Formats.Generation`) takes the
entrants' keys in order, the parameters (left out: the default) and a `ulong` seed, and returns the format's code and
version, every parameter, the seed, the entrants as given, the stages with their rounds and pairings, and
`OutputHash`: `sha256:` over RFC 8785 canonical JSON of all of these (the seed as a decimal string, since a JSON
number rounds a 64-bit seed), so the hash identifies the draw, not only its structure (owner, S05-T3b plan). A pairing
is coded `<round code>_<n>`; its sides are an entrant, `WINNER_OF` or `LOSER_OF` an earlier pairing, or a bye (always
away; no contest). League rounds are `ROUND_<n>`; knockout rounds are `ROUND_OF_<n>`, `QUARTER_FINALS`,
`SEMI_FINALS`, `FINAL` by the bracket's places left, byes counted (`BracketPlaces`: 5 entrants start in
`QUARTER_FINALS` with 8; review #83), and the third-place contest is its own round `THIRD_PLACE` just before
the final. `RANDOM` uses the module's own PRNG (xoshiro256** seeded by SplitMix64, unbiased bounded draws,
Fisher–Yates), pinned to the published outputs. Errors: `FORMAT_PARTICIPANT_COUNT_INVALID` (outside the format's
`participants`), `FORMAT_PARAMETER_INVALID` (unknown, wrong type, out of range, an unknown choice; a third-place
contest with fewer than 4 participants) and `FORMAT_ENTRANTS_INVALID` (a blank, untrimmed or repeated key; a bye
outside `MANUAL` knockout lines; `MANUAL` lines not the bracket size for their entrants, or two byes in one pairing);
the message names what is wrong. The round robin is de Werra's canonical schedule (the circle method with the fixed
place at home in even rounds, `r+k` at home for odd `k`), which keeps each entrant's home and away within one per
cycle. The fixed place is the last entrant (the circle method above fixes the first; either gives the same schedule up to
numbering), and with an odd count the bye takes it. Generators are pure (architecture test).

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
  `MANUAL` input is the bracket lines in order, bracket size long, with explicit byes; two byes in one pairing are
  refused. A bye pairing is listed in the first round without a contest, and the next round's slot is already its
  entrant (owner, S05-T3 plan). In a round robin, `MANUAL` keeps the order given, as `SEEDED` does.
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
