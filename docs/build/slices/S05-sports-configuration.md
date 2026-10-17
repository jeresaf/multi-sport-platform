# S05 — Sports configuration (football first)

**Goal:** sports become data plus an engine: the football definition is seeded and immutable, the ADDITIVE
engine computes scores and outcomes from events, fixture generators work, ruleset templates resolve into an
effective-rules snapshot, and tenants enable football within their plan.
**Modules touched:** Sports Configuration (new), Tenancy (sport enablement), admin web.
**Depends on:** S04.
**Demo:** admin browses the football definition (periods, events, positions, roles, statistics, scorer pad
preview); engine scenario tests read like match reports; a round-robin draw for 7 teams is generated
deterministically; the football league template resolves with edited points into a snapshot; tenant enables
football and hits the sport quota on a STANDARD plan.

Read first: ADR-0003, ADR-0006, docs/architecture/sport-engine.md, docs/domain/sports/README.md and
football.yaml, docs/domain/formats.md, docs/domain/rulesets.md.

## Out of scope

Persisted timelines and contests (S10–S12), other sports (S15–S17; their reference YAML stays in docs until then),
in-app editors (ADR-0006).

## Tasks

### T1 — Definition schema, loader and immutability
Deliver:
- `Modules/SportsConfiguration` (+ Contracts). Backend copy of `football.yaml` as an embedded resource.
- JSON Schema for definitions (from docs/domain/sports/README.md) + semantic validation: unique codes; every
  referenced event, actor role, period, position group and statistic exists; scorer pad references defined events;
  follow-up and reversal references valid.
- `config.sport_definitions` (variant, sport, version, status, content `jsonb`, content_hash, published_at).
  CONFIGURATION seeder: parse → validate → canonical JSON → SHA-256 → insert; if `(variant, version)` exists with
  a **different** hash, fail loudly (`SPORT_DEFINITION_IMMUTABLE`) — CI test.
- Queries (`ISportDefinitions`): list variants, get resolved definition by variant (+ version), terminology.
  Read endpoints for admin (`sport.read`) and public metadata needed by renderers.

Acceptance:
- [ ] Changing any byte of the published football definition without bumping `version` fails a test.
- [ ] Validation errors name the file, path and problem.

### T2 — ADDITIVE engine, fold and scenario runner
Deliver:
- `IScoringEngine` + generic `Fold` + `ScoreSummary` per sport-engine.md; `AdditiveEngine` implementing §5:
  points/side/component, phase rules, actor requirements by coverage, personnel tracker (lineup, substitution,
  send-off, temporary suspension, return), follow-up constraints, rule limits, tie resolution incl.
  `extra_period_sequence`, sudden death and shoot-out.
- Scenario runner: YAML files under `tests/SportsPlatform.UnitTests/Engines/Scenarios/football/` with definition
  version, rule overrides, events and expected summary/outcome/validation errors.
- At least 25 football scenarios, including: normal win; draw; own goal; penalty goal and miss; VAR overturn
  (reversal); scorer correction (supersede); assist equals scorer (rejected); scoring during half-time (rejected);
  shoot-out event during play (rejected); actor not on the field at STANDARD (rejected) but allowed at BASIC;
  substitution limit; red card then sent-off player as scorer (rejected); cup tie → extra time → shoot-out win;
  league draw stays a draw; sudden-death rule using a test-only definition with a `sudden_death` period.
- Error codes: `TIMELINE_EVENT_UNKNOWN`, `TIMELINE_EVENT_NOT_ALLOWED_IN_PHASE`, `TIMELINE_ACTOR_REQUIRED`,
  `TIMELINE_ACTOR_NOT_ON_FIELD`, `TIMELINE_ACTOR_INVALID`, `TIMELINE_FOLLOW_UP_MISSING`, `TIMELINE_LIMIT_REACHED`.

Acceptance:
- [ ] Engine has no I/O, clock or randomness (architecture test on dependencies).
- [ ] Every rule in sport-engine.md §5 has a passing and a rejecting scenario.

### T3 — Formats and generators
Deliver:
- Format definitions as embedded YAML: `SINGLE_CONTEST`, `ROUND_ROBIN`, `KNOCKOUT` (docs/domain/formats.md);
  seeded like sport definitions (hash-checked).
- Pure generators: round robin (circle method, byes, cycles, home/away balance), knockout (bracket size, byes to
  top seeds, standard seed placement, optional third-place contest), single contest. Inputs: ordered entrants,
  parameters, seed. Output: stages/rounds/pairings/slots + output hash.
- Tests: for n = 2…20 round robin — each pair meets `cycles` times, one contest per entrant per round (except bye),
  home/away difference ≤ 1 per cycle, deterministic for a seed; knockout n = 2…64 — seeds 1 and 2 only meet in the
  final, byes only to top seeds, slot references consistent.

Acceptance:
- [ ] Generators exposed via Contracts (`IFormatGenerators`) with no dependency on other modules.

### T4 — Ruleset templates and resolver
Deliver:
- Templates as embedded YAML: `FOOTBALL_LEAGUE`, `FOOTBALL_CUP` (docs/domain/rulesets.md) with parameter types,
  defaults and editable bounds; tiebreaker catalogue codes defined.
- `IRulesetResolver.Resolve(template, overrides, formatParameters)` → validated effective-rules snapshot (JSON +
  hash + exact versions). Validation errors: `RULESET_PARAMETER_UNKNOWN`, `RULESET_PARAMETER_OUT_OF_RANGE`,
  `RULESET_INCOMPATIBLE_FORMAT`, `RULESET_UNKNOWN_TIEBREAKER`, `RULESET_TIE_RESOLUTION_UNSUPPORTED`.
- Admin endpoint to preview a resolution (used by the S09 wizard).

Acceptance:
- [ ] Same inputs → same snapshot hash (test); invalid combinations rejected with the right codes.

### T5 — Tenant sport enablement and admin views
Deliver:
- `tenancy.tenant_sports` (tenant-owned, RLS): `tenant.sport.enable` / `tenant.sport.disable` gated by
  `MAX_ENABLED_SPORTS`, which counts distinct sports (two variants of the same sport, e.g. rugby 15s and 7s,
  count once); disabling never invalidates history (only blocks new editions).
- Admin screens: Sports (list) → definition viewer (tabs: Overview, Periods, Events, Positions & roles,
  Statistics, Scorer pad preview rendered from metadata), Formats, Rules templates — all read-only; tenant
  settings → Sports (enable/disable).
- Architecture test: no sport codes (e.g. `"FOOTBALL"`) as string literals outside definitions, engines,
  renderers and tests.

Acceptance:
- [ ] STANDARD tenant can enable up to its limit; the next enable shows "Requires PRO" (or limit reached).
- [ ] Scorer pad preview is built from the YAML, not hard-coded.

## Slice acceptance

- [ ] Football is fully described by data; the ADDITIVE engine passes all scenarios; generators and resolver are
      deterministic; tenants enable sports within entitlements.
- [ ] docs/architecture/sport-engine.md updated if implementation clarified anything.
