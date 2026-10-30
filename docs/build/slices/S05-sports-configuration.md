# S05 — Sports configuration (football first)

> Rewritten by the S04 slice review (2026-10-30) to match what S01–S04 actually built. It replaces the kit's version.
> Changes from the kit's version: the decisions below (D1–D22) are written out; only football is seeded, so the
> sport quota is proven with test-only definitions and shown in the demo through an override (owner, 2026-10-30;
> D18); definitions, formats and templates can be read in the tenant workspace and in the console, and the public
> renderer metadata waits for S12/S14 (owner, 2026-10-30; D16); the engine works on in-memory events, its input types
> (named as in ADR-0003) owned by Sports Configuration until timelines arrive in S12 (D7); sport enablement keeps a
> row per enablement and has no DEMO seeder yet (D12, D19); the YAML and JSON Schema libraries are named with their licences
> (D2); Q27's new current-period rule rides T5, which touches Tenancy (owner, 2026-10-30); task sizes are estimated
> and the likely PR splits named.

**Goal:** sports become data plus an engine: the football definition is seeded, hashed and immutable; the ADDITIVE
engine computes scores and outcomes from events; the fixture generators are pure and deterministic; ruleset templates
resolve into an effective-rules snapshot; tenants enable football within their plan.
**Modules touched:** Sports Configuration (new: definitions, engines, formats, generators, templates, resolver),
Tenancy (sport enablement, `MAX_ENABLED_SPORTS`, Q27), Identity (roles for the new actions), Host (catalogues,
startup rules), admin web.
**Depends on:** S04.
**Demo:** a tenant administrator of KMSA opens **Sports** in the workspace → opens Football: Overview, Periods,
Events, Positions & roles, Statistics, and the scorer pad preview built from the definition → Formats (single
contest, round robin, knockout) and Rules templates (`FOOTBALL_LEAGUE`, `FOOTBALL_CUP`) with their editable
parameters → Settings → Sports: enables football ("1 of 3 sports") → a platform administrator opens ELSSC and sets
the override `MAX_ENABLED_SPORTS = 0` with a reason → ELSSC's owner tries to enable football and is refused with
"Your plan allows 0 sports" (`403 ENTITLEMENT_QUOTA_EXCEEDED`) → the admin removes the override and the enable
goes through → the KMSA administrator disables football with a reason and enables it again (both enablements kept
in the history). The engine, generators
and resolver are shown by their tests: football scenarios that read like match reports, a round robin for 7 teams
drawn twice with one seed and giving one hash, the league template resolved with 4 points for a win into a snapshot
whose hash is stable.

Read first: ADR-0003, ADR-0006, docs/architecture/sport-engine.md, docs/domain/sports/README.md and football.yaml,
docs/domain/formats.md, docs/domain/rulesets.md, docs/architecture/rules.md (AR-11–AR-14, AR-29, AR-30),
docs/architecture/data-conventions.md ("Seeding"), docs/architecture/authorization.md ("Entitlements").

## What S01–S04 give you (use it, don't rebuild it)

- **Modules:** `IModule`, module registration in the Host, one schema and DbContext per module
  (`ModuleDbContext`, its own migrations history), `ReadOnlyForRuntimeRoles` + `TableAccessPolicies` for global
  tables, `ITenantOwned` + `EnableTenantRls` + isolation samples (`IsolationSamples`, `RlsCoverageTests`) for tenant
  tables, `RowVersion`/`If-Match`. Architecture tests: `ModuleBoundaryTests` (only `*.Contracts` across modules),
  `LayeringTests` (Domain free of EF Core and ASP.NET Core).
- **Seeding:** `ISeeder`/`AddSeeder<T>()`, categories, `Checksum`, `DeterministicIds`, `platform.seed_runs`. The
  first `CONFIGURATION` seeder is Tenancy's `PlanSeeder` (`tenancy.configuration.plans`, as the owner);
  `PostgresFixture` seeds `REFERENCE` and `CONFIGURATION` once and keeps their tables across resets.
- **Actions and catalogues:** `ActionDefinition` (`Entitlement`, `AvailableWhenBlocked`, `AvailableWhenReadOnly`; a
  startup rule requires `availableWhenReadOnly` on every tenant `.read`), `EntitlementRules`, `export-catalogues`
  (actions, entitlements, roles, errors, events), the `Contracts` CI job. Roles are explicit lists in Identity's
  `IdentityRoles`; an action no role grants fails the matrix test.
- **Entitlements:** `MAX_ENABLED_SPORTS` (QUOTA, unit `sports`, STANDARD 3, PRO unlimited, enforced by "Tenancy &
  Commercial") is declared and seeded; no action names it yet. `IEntitlements.CheckQuotaAsync(code, used)` refuses
  at the limit with `ENTITLEMENT_QUOTA_EXCEEDED` and warns at 80 %; `AddQuotaUsage<TCounter>(code)` registers the
  counter the usage reads use (Identity's `StaffUsers` is the model, with its lock `identity.staff_users:<tenantId>`
  and the endpoint adding "Your plan allows N staff users."). The Host documents `ENTITLEMENT_QUOTA_EXCEEDED` on every
  action naming a quota. Overrides (`entitlement.override`) already let platform staff set any quota.
- **Pipeline:** steps 1–3 run for every tenant action; capabilities list `actions` and `unavailable` (read-only,
  entitlement) and `access`.
- **Admin web:** shell and navigation filtered by capabilities (`lib/navigation.ts`), `useTenantWorkspace().can()`
  and `.unavailable()` with `UnavailableNote`, `ActionDialog` (reasons), `ConflictAlert`, `describeProblem` (shows a
  quota refusal's `detail`), `QuotaUsage`, the console's Plans page and tenant Subscription panel, Settings →
  Subscription. Playwright against the real API; test tenants of their own per spec (pilot-customer.md).

## Decisions (reviewer defaults; confirm or change in each task's plan)

- **D1 — The module.** `Modules/SportsConfiguration/SportsPlatform.Modules.SportsConfiguration` (+ `.Contracts`),
  schema `config` (ADR-0006), depending on Platform only (modules.md). Global data only: no `tenant_id`, no RLS. Tenancy
  references `SportsConfiguration.Contracts` (modules.md: "read: sport variants for enablement"); nothing else does
  yet.
- **D2 — Files and libraries.** The backend's copy of `docs/domain/sports/football.yaml` and the format and template
  files are embedded resources under `Definitions/Sports`, `Definitions/Formats`, `Definitions/Rulesets`. YAML is
  read with **YamlDotNet** (MIT) into a JSON tree and checked against JSON Schema files (`*.schema.json`, embedded
  next to them) with **JsonSchema.Net** (MIT), as ADR-0006 asks; semantic checks are plain code (D3). The PR lists
  both packages with their licences. The docs copy and the backend copy stay equal: a test compares them, and a
  change updates both (docs/domain/sports/README.md).
- **D3 — Validation.** Shape by JSON Schema; then semantic rules in code: unique codes per list; every reference
  resolves (event → actor role, period, position group, statistic → event and actor role, scorer pad → event,
  follow-up and `reversible_by` → event, `supports.tie_resolutions` → known values); a `SHOOTOUT` tie resolution
  needs a `SHOOTOUT` period; every score component an event names other than `main` is one the family knows
  (`shootout`). Every error names the file, the
  JSON path and the problem (`football.yaml: $.event_types[3].actors[0].role: unknown actor role "SCORERR"`). The
  seeder, the startup check and a unit test all run the same validator; a bad file stops the Host at startup.
- **D4 — Stored, hashed, immutable.** `config.sport_definitions` (variant, sport, version, name, scoring
  family, shape, status `PUBLISHED`, content `jsonb`, content hash, published at; unique `(variant, version)`), and
  the same pattern for `format_definitions` (code, version) and `ruleset_templates` (code, version, sport variant,
  formats). Read-only for runtime roles. The hash is SHA-256 over the content in RFC 8785 canonical JSON (JSON
  Canonicalization Scheme: sorted keys, no insignificant whitespace, one form per number) with the scheme in the stored value (`sha256:…`). The `CONFIGURATION` seeders
  (`config.configuration.sports`, `.formats`, `.rulesets`; order sports → formats → rulesets) insert a missing
  `(code, version)` and fail loudly with `SPORT_DEFINITION_IMMUTABLE` (`FORMAT_DEFINITION_IMMUTABLE`,
  `RULESET_TEMPLATE_IMMUTABLE`) when one exists with a different hash; seeders never update or delete. A test changes
  one byte of the embedded football file without bumping `version` and expects the failure. A unit test pins
  football@1's hash, so an accidental edit fails before any database.
- **D5 — Queries in Contracts.** `ISportDefinitions` (list variants with name, sport, family, shape, latest version;
  get a resolved definition by variant and optional version, generic event types merged in from README.md's table;
  terminology), `IFormatDefinitions`, `IRulesetTemplates`. Read from the database (seeded rows are the truth),
  cached in process until restart (configuration never changes at runtime). The resolved definition is a typed,
  read-only model (records), never `JsonElement` in Contracts.
- **D6 — No sport words in generic code.** Generic code reads variants as data. An architecture test fails on string
  literals of the known sport and variant codes (`FOOTBALL`, `FOOTBALL_ASSOCIATION`, `RUGBY_UNION_15`, …, read from
  the definition files and README.md's table) outside `Definitions/`, the engines, renderers and tests (AR-11). The
  web gets the same rule through ESLint (`no-restricted-syntax` on those literals in `apps/` outside renderers).
- **D7 — The engine and its input types.** `IScoringEngine` per ADR-0003 and sport-engine.md §4 in Sports
  Configuration Contracts, with the contract's types under the ADR's names, owned by Sports Configuration Contracts:
  `TimelineEventDraft` and `TimelineEvent` (sequence, type, side, period code and occurrence, clock, actors by role,
  payload, `supersedes`/`reverses`), `ContestLiveState`, `ContestContext` (definition, rules snapshot, sides, coverage,
  lineups). Timeline rows don't exist until S12; Match Operations maps its rows to these types then.
  `IScoringEngines` picks the engine by the definition's `scoring_family`, never by sport. The generic `Fold`, phase
  tracker, personnel tracker, follow-up and limit checks live next to the engines (shared by every family). Engines
  have no I/O, clock or randomness: an architecture test allows them only BuildingBlocks' `Result`/`Error` and the
  module's Contracts.
- **D8 — Validation errors.** The engine returns `Error`s from `SportsConfigurationErrors` in the `TIMELINE_` area
  (the codes the scorer will see in S12): `TIMELINE_EVENT_UNKNOWN`, `TIMELINE_EVENT_NOT_ALLOWED_IN_PHASE`,
  `TIMELINE_ACTOR_REQUIRED`, `TIMELINE_ACTOR_NOT_ON_FIELD`, `TIMELINE_ACTOR_INVALID`, `TIMELINE_FOLLOW_UP_MISSING`,
  `TIMELINE_LIMIT_REACHED`, plus `TIMELINE_CONTEST_NOT_LIVE` and `TIMELINE_REFERENCE_INVALID` (a `supersedes` or
  `reverses` to an unknown or already reversed event) if the scenarios need them (all 422). They're in the error
  catalogue from S05; no endpoint returns them yet. Match Operations declares its own `TIMELINE_*` codes in S12
  (`TIMELINE_IDEMPOTENCY_CONFLICT`): a prefix names an area, not a module, and codes are unique in the catalogue.
- **D9 — Scenarios.** YAML under `backend/tests/SportsPlatform.UnitTests/Engines/Scenarios/football/`: definition
  version, rule overrides, coverage, lineups, events (with minute, side, actors by name), expected score summary,
  outcome and validation errors per event. Names are KMSA's fictional clubs and invented players
  (pilot-customer.md), so the owner and federation experts can read them (sport-engine.md §11). Each of §5's rules
  has a passing and a rejecting scenario; a test lists the rules and fails when one has neither.
- **D10 — Formats and generators.** Format files for `SINGLE_CONTEST`, `ROUND_ROBIN`, `KNOCKOUT` (formats.md), seeded
  and hashed like definitions. `IFormatGenerators` (Contracts): pure functions of ordered entrant keys, parameters
  and a seed → stages, rounds, pairings (home, away or bye) and slots (`Winner of QF1`), with an output hash. The
  seed is an input (`RANDOM` seeding uses a seeded PRNG of the module's own, not `System.Random`'s unspecified
  algorithm, so a stored seed reproduces the draw across .NET versions). A count the format can't handle is
  `FORMAT_PARTICIPANT_COUNT_INVALID`; unknown or out-of-range parameters are `FORMAT_PARAMETER_INVALID`.
  `GROUPS_THEN_KNOCKOUT` waits for S15 and `SERIES` for S16 (their sports need them first).
- **D11 — Templates and the resolver.** `FOOTBALL_LEAGUE` and `FOOTBALL_CUP` (rulesets.md), with typed parameters,
  defaults and editable bounds; the tiebreaker catalogue's codes are declared in Sports Configuration (Standings
  computes them in S13). `IRulesetResolver.Resolve(template, overrides, formatParameters)` → `Result<RulesSnapshot>`
  (canonical JSON, hash, exact sport/format/template versions). Errors: `RULESET_PARAMETER_UNKNOWN`,
  `RULESET_PARAMETER_OUT_OF_RANGE`, `RULESET_INCOMPATIBLE_FORMAT`, `RULESET_UNKNOWN_TIEBREAKER`,
  `RULESET_TIE_RESOLUTION_UNSUPPORTED` (each names the parameter). Same inputs → same hash. Snapshots are not stored
  in S05 (editions freeze them in S09); a preview endpoint returns one (D16).
- **D12 — Sport enablement in Tenancy.** `tenancy.tenant_sports` (tenant-owned, RLS, isolation sample), one row per
  enablement: variant, enabled by/at, disabled by/at and why. A row changes only to be closed (disabled); enabling
  again adds a new row, so the history stays in the table (AR-18), as in `tenant_entitlement_overrides`; a partial
  unique index allows one open row per tenant and variant. Actions `tenant.sport.enable` (names `MAX_ENABLED_SPORTS`; audit `REQUIRED`) and
  `tenant.sport.disable` (`REQUIRED_WITH_REASON`), for `TENANT_OWNER` and `TENANT_ADMIN`. The variant is checked
  through `ISportDefinitions` (`TENANT_SPORT_VARIANT_UNKNOWN`, 400 on the field). Enabling an enabled variant or
  disabling a disabled one is `409 TENANT_SPORT_ALREADY_ENABLED` / `TENANT_SPORT_NOT_ENABLED`. Disabling never touches
  history (S09 blocks new editions of a disabled variant). Tenancy provides `IEnabledSports` (Contracts) for S06–S09.
- **D13 — `MAX_ENABLED_SPORTS`.** Counts distinct `sport` values of the tenant's enabled variants (open rows), read through
  `ISportDefinitions` (`RUGBY_UNION_15` + `RUGBY_UNION_7` = 1). Enable holds `tenancy.tenant_sports:<tenantId>` while
  it counts and saves; enabling a variant whose sport is already counted adds nothing, so it is never refused.
  Tenancy registers the counter with `AddQuotaUsage`, so the Subscription page and the console's panel show "1 of 3
  sports" as they do for staff users. The refusal's `detail` says "Your plan allows N sports." (as for staff).
  Lowering the limit never disables a sport.
- **D14 — Actions and roles.** Tenant reads: `sport.read`, `format.read`, `ruleset.read` (definitions, formats,
  templates and the preview; every tenant role, like `tenant.settings.read`, since everyone works with the sport's
  words; available when read-only; audit `NONE`), `tenant.sport.read` (the tenant's enabled variants; every tenant
  role). Platform reads: `platform.sport.read` (definitions, formats, templates; `PLATFORM_ADMIN`,
  `PLATFORM_SUPPORT`; audit `NONE`: global configuration, no tenant data). Names follow docs/generated/actions.md's
  existing patterns; the T1 plan confirms them.
- **D15 — The tenant's sports read.** `GET /api/admin/v1/tenant/sports`: every published variant with its name,
  sport, family and whether the tenant has it enabled (and since when), plus the sport quota's usage for those who
  may read the subscription (`tenant.subscription.read`) — others see the list only.
- **D16 — Endpoints.** Admin (tenant): `GET /api/admin/v1/sports`, `GET …/sports/{variant}` (`?version=`, latest by
  default), `GET …/formats`, `GET …/formats/{code}`, `GET …/ruleset-templates`, `GET …/ruleset-templates/{code}`,
  `POST …/ruleset-templates/{code}/preview` (body: overrides and format parameters → the snapshot or the resolver's
  errors on their fields; a read: `ruleset.read`, no save), `GET …/tenant/sports`,
  `POST …/tenant/sports/{variant}/enable`, `POST …/tenant/sports/{variant}/disable` (reason). Platform: `GET
  /api/platform/v1/sports`, `…/sports/{variant}`, `…/formats`, `…/ruleset-templates` (same DTOs). Definitions answer
  with `ETag` from the content hash. No public endpoint in S05: the renderers' metadata comes with its first
  consumer, the live match page (S12) or the public pages (S14) (owner, 2026-10-30; AR-30). List query parameters are
  typed in OpenAPI from the start (S03 follow-up).
- **D17 — Screens.** Tenant workspace: **Sports** (navigation; `sport.read`) → variant page with tabs Overview,
  Periods, Events, Positions & roles, Statistics, Scorer pad; **Formats** and **Rules templates** pages (parameters
  with type, default and bounds) — all read-only. Settings → **Sports** (`tenant.sport.read`): each variant with
  Enable/Disable (reason), the quota's usage and the 80 % warning, Enable disabled with "Your plan allows N sports."
  at the limit (as T4b2 did for invitations). Console: **Sports** with the same viewer, read-only. Labels and
  terminology come from the definition (AR-29); nothing in the UI names football.
- **D18 — The sport quota without a second sport (owner, 2026-10-30).** Only football is seeded in S05; rugby,
  basketball and cricket definitions are seeded by S15–S17 with their templates and scenarios. Integration tests
  prove the counting with test-only definitions registered by a test app (two variants of one test sport and a second
  test sport, as `EntitlementTestApp` registers `FIXTURE_PUBLISHING`): enable both variants of one sport on a limit
  of 1, and the second sport is refused. The demo shows the refusal through the override `MAX_ENABLED_SPORTS = 0`.
  S04's D20 and Q25 said S05's sport quota would be the first real "Requires PRO": it can't be (a quota refusal says
  the limit, not a plan, D14 of S04; and no STANDARD tenant can pass 3 sports before cricket, S17). The first real "Requires PRO" is
  `tenant.domain.add` (S22).
- **D19 — Seeds and test data.** No DEMO seeder in S05: the manual demo enables football on screen (KMSA, ELSSC).
  The first slice whose demo data needs a sport (S07's teams) adds `tenancy.demo.sports` (through
  `tenant.sport.enable` as `SYSTEM`, skipped when enabled). Tests: `TenantTestData` can enable sports for a tenant;
  `PostgresFixture` keeps `config`'s tables with the other
  configuration tables. e2e: the demo spec runs on a test tenant of its own (as `admin-plans-demo.spec.ts` does).
- **D20 — No jobs, no integration events.** Nothing consumes sport enablement yet (S09 reads `IEnabledSports`
  directly); add an event with its first consumer.
- **D21 — Q27: the current period with none started (owner, 2026-10-30).** With no period started, the current
  period is the **earliest** non-cancelled scheduled period; if every period is cancelled, the latest of any kind, as
  today. The plan that applies follows it (S04-T3a2: the current period's plan), so a scheduled-only tenant gets the
  earliest period's plan. T5 changes `TenantSubscription`, state-machines.md and the tests of the current-period
  table and of `IEntitlements`, and checks the banner and the Subscription page against two scheduled periods; the
  PROGRESS.md tech-debt item goes.
- **D22 — Docs.** sport-engine.md "as built" notes (engine input types, where the trackers live, the hash scheme),
  formats.md and rulesets.md where the build clarified them, modules.md (Sports Configuration provides as built;
  Tenancy's `IEnabledSports`), authorization.md (the sport actions and `MAX_ENABLED_SPORTS` as built),
  admin-app.md (Sports screens), docs/domain/sports/README.md (the backend copy and the equality test),
  pilot-customer.md (demo sports), CLAUDE.md (any new command).

## Out of scope

Persisted timelines, contests and snapshots (S09–S12); other sports' definitions, templates and scenarios
(S15–S17); `GROUPS_THEN_KNOCKOUT` (S15), `SERIES` (S16); in-app editors (ADR-0006); public sport endpoints (D16);
statistics computation (S13+); `LIST` entitlements such as `ALLOWED_SPORTS`; tenant-custom sports.

## Tasks

### T1 — Module, definition schema, loader, seeding and reads (~3 PRs)
Deliver:
- The module (D1), football's backend copy, JSON Schema, validator (D2, D3), the three tables' first one
  (`sport_definitions`), the sports seeder with the immutability check (D4), `ISportDefinitions` (D5), the
  architecture test on sport literals (D6, backend part).
- Reads: `GET /api/admin/v1/sports`, `…/sports/{variant}`, the platform pair (D14, D16); new actions in the roles.
- Likely T1a (module, schema, validator, unit tests, the docs-copy test), T1b (table, seeder, hash, reads,
  catalogues).

Acceptance:
- [x] Changing any byte of the published football definition without bumping `version` fails a test (seeder and
      pinned hash). (T1b1: `SportDefinitionSeedTests`, `ReferenceSportFilesTests`; content bytes, not comments: owner,
      2026-10-30)
- [x] Validation errors name the file, path and problem (one test per rule in D3). (T1a: `SportDefinitionValidatorTests`)
- [x] A bad definition stops the Host at startup (test). (T1a: `DefinitionStartupCheckTests`)

### T2 — ADDITIVE engine, fold and scenarios (~3 PRs)
Deliver:
- `IScoringEngine`, `IScoringEngines`, engine input types, `Fold`, `ScoreSummary` (D7); the phase and personnel
  trackers, follow-ups and limits; `AdditiveEngine` per sport-engine.md §5 including tie resolution with
  `extra_period_sequence`, sudden death and shoot-out; the errors (D8).
- The scenario runner and at least 25 football scenarios (D9), including: normal win; draw; own goal; penalty goal
  and miss; VAR overturn (reversal); scorer correction (supersede); assist equals scorer (rejected); scoring at
  half-time (rejected); shoot-out event during play (rejected); actor not on the field at `STANDARD` (rejected) but
  allowed at `BASIC`; substitution limit; sent-off player as scorer (rejected); cup tie → extra time → shoot-out
  win; league draw stays a draw; sudden death with a test-only definition that has a `sudden_death` period.
- Likely T2a (contract, fold, trackers, runner), T2b (scoring, phases, actors, follow-ups, limits), T2c (outcome
  and tie resolution).

Acceptance:
- [ ] The engines have no I/O, clock or randomness (architecture test on dependencies).
- [ ] Every rule in sport-engine.md §5 has a passing and a rejecting scenario (a test lists them).

### T3 — Formats and generators (~2 PRs)
Deliver:
- Format files, schema, table, seeder (D4, D10); `IFormatDefinitions`; the reads (D16).
- Generators: round robin (circle method, byes, cycles, home/away balance), knockout (bracket size, byes to top
  seeds, standard seed placement, optional third-place contest, slots), single contest; output hash.
- Tests: round robin for n = 2…20 (each pair meets `cycles` times, one contest per entrant per round except a bye,
  home/away difference ≤ 1 per cycle, deterministic for a seed); knockout for n = 2…64 (seeds 1 and 2 meet only in
  the final, byes only to top seeds, slot references consistent); a pinned hash for a 7-entrant round robin.

Acceptance:
- [ ] Generators are exposed through Contracts (`IFormatGenerators`) and depend on no other module (architecture
      test).
- [ ] A stored seed reproduces the same draw and hash (test).

### T4 — Ruleset templates and resolver (~2 PRs)
Deliver:
- Template files, schema, table, seeder (D4, D11); the tiebreaker catalogue codes; `IRulesetTemplates`,
  `IRulesetResolver`; the reads and the preview endpoint (D16).

Acceptance:
- [ ] Same inputs → same snapshot hash (test); every invalid combination is refused with its code, naming the
      parameter (table-driven).

### T5 — Tenant sport enablement, Q27 and the screens (~3 PRs)
Deliver:
- `tenancy.tenant_sports` (isolation sample), enable/disable with the quota under the lock, the usage counter,
  `IEnabledSports`, `GET …/tenant/sports` (D12, D13, D15); the D18 test app; test data (D19).
- Q27's rule (D21).
- Screens (D17): Sports and the variant viewer, Formats, Rules templates, Settings → Sports, the console's Sports;
  generated client; `describeProblem` for the new codes; ESLint sport-literal rule (D6).
- Playwright: the slice demo against the real API; screenshots of the new screens at phone, tablet and desktop
  widths, light and dark. Slice close.
- Likely T5a (enablement, quota, Q27), T5b1 (viewer screens), T5b2 (settings, console, demo e2e, slice close).

Acceptance:
- [ ] A STANDARD tenant enables up to its limit; two variants of one sport count once; the next sport is refused
      with `403 ENTITLEMENT_QUOTA_EXCEEDED` and "Your plan allows N sports." (integration tests, D18).
- [ ] Two enables at the last free sport at the same time: one succeeds, one is refused (test).
- [ ] The scorer pad preview is built from the definition, not hard-coded (Vitest with a test definition).
- [ ] With two scheduled periods and none started, the current period and its plan are the earlier one's (table and
      `IEntitlements` tests), and the banner and the Subscription page say so (Vitest).
- [ ] Disabling and enabling again keeps both enablements in `tenancy.tenant_sports` (test reads the table).

## Carried over from S04 (slice review 2026-10-30)

Each is assigned above; remove it from PROGRESS.md "Tech debt" when its task merges.
- Q27: with two scheduled periods and none started, the banner says "until" the later start (#72 R2, R4) — T5a (D21).
- `renewalStartsOn` in the platform subscription read (#71 R5) stays with its own trigger (the next task that changes
  that read); S05 doesn't touch it.

## Slice acceptance

- [ ] Football is fully described by data; the ADDITIVE engine passes all scenarios; generators and the resolver are
      deterministic; tenants enable sports within their entitlement.
- [ ] Sport definitions, formats and templates are immutable once seeded (tests); tenant sports are covered by the
      isolation harness; the RLS exception list is unchanged.
- [ ] No sport code appears in generic backend or web code (architecture test, ESLint).
- [ ] Docs updated (D22); the S04 carry-overs above are done and removed from PROGRESS.md.

## Implementation notes

(The builder adds notes here as tasks merge.)

- **T1a:** JsonSchema.Net is pinned to 8.0.5, its last MIT release (owner, 2026-10-30; PROGRESS.md "Deviations").
  Definition problems are bugs in shipped files, not API errors, so they have no catalogue code. The schema and the
  rules also accept the rugby and basketball reference files (a test checks this). The scoring families the
  validator knows, their score components and their tie resolutions are in `Definitions/ScoringFamilies`; T2's
  engines should read them from there.
- **T1b1:** `config.sport_definitions` stores the content as `jsonb` written from its RFC 8785 canonical text, with its
  hash; the hash ignores comments and layout (owner, 2026-10-30). The seeder validates each file and throws
  `SPORT_DEFINITION_IMMUTABLE: …` for a changed version and `SPORT_DEFINITION_DUPLICATE_VERSION: …` for two files
  declaring one (seed failures, not catalogue errors, like T1a's definition problems). `ISportDefinitions` resolves rows
  into `SportDefinition` records: closed vocabularies are enums (`CoverageLevel` is ordered), `scoring_family` stays a
  string (it keys `ScoringFamilies`), the file's `short` is `ShortLabel`, `metadata` field types are `Choice`, `Flag`,
  `Number`, `Text` (`enum`, `bool`, `int`, `string` in the files), and `derive.where` values are text as written. The
  model refuses keys it doesn't map. Generic event types are a separate list with English labels written from their
  codes. Nothing is cached before the first seed. The module's concurrency error is `SPORT_CONCURRENCY_CONFLICT`
  (`SPORT_` is a documented area). The records carry the file's JSON names (`short`, `enum`, …) and hide the hash and
  the generic types from the file reader, so T1b2's endpoints map them to response DTOs of their own.
- **T1b2:** the reads are `GET /api/admin/v1/sports` and `…/sports/{variant}?version=` (`sport.read`) and the same
  under `/api/platform/v1` (`platform.sport.read`), one handler and one DTO each. `SportDefinitionResponse` maps the
  typed definition into API names: camelCase, UPPER_SNAKE values, `shortLabel` for the file's `short`, metadata types
  `CHOICE`, `FLAG`, `NUMBER`, `TEXT`, ruleset limits as their rule strings; actor roles, metadata fields and `derive.where`
  stay maps keyed by code. The definition's `ETag` is its quoted `contentHash` (no `If-None-Match` handling yet). An
  unknown variant or version is `SPORT_DEFINITION_NOT_FOUND`; variant codes match exactly. `TEAM_MANAGER` has no
  `sport.read` until S06; `format.read` and `ruleset.read` come with T3 and T4 (owner, 2026-10-30).
- **T2a:** the engine contract and its input types are in Contracts' `Engines` (sport-engine.md §4, "As built
  (S05-T2a)"); `ScoringFamilies` moved there from `Definitions` so the engines and the validator share it
  (owner, 2026-10-30). `IScoringEngines.EngineFor(definition)` (CA1716 refuses `For`) and `Fold(ctx, status,
  timeline)`. Family state is behind the abstract `ScoreState`; `AdditiveScoreState` is public in the module for tests
  only. `ContestRules` is the effective rules flattened to dotted paths. Until T4's resolver, scenarios start from
  `Scenarios/rules/<TEMPLATE>.yaml` (the rulesets.md values) plus their own `rule_overrides` (owner, 2026-10-30).
  Scenario files: `title`, `definition`, `rules`, `rule_overrides`, `coverage`, `status` (default `LIVE`), `home` and
  `away` (team, starters, bench, staff), `events` (`minute` as `23` or `45+2`, `type`, `team`, `period`, `actors` by
  role, `payload`, `id`, `supersedes`/`reverses` by id, `rejected` error codes), `expect` (score per team: `total`,
  `shootout`, `periods` by short label; `outcome`: `winner` and `method`, or `draw`). T2a declares only
  `TIMELINE_EVENT_UNKNOWN`; the other D8 codes come with T2b's checks. T2b: points overrides by rule, and a participant
  that isn't in the contest (Apply throws on a scoring event without one).
- **T2b1:** plan approved with the recommended answers (owner, 2026-10-30): the caller says whether the correction
  window is open (`ContestLiveState.CorrectionWindowOpen`); a correction is checked as at the event it replaces; the
  new code `TIMELINE_PARTICIPANT_INVALID`; T2b split into T2b1 (status, references, phase, participant, actors) and
  T2b2 (follow-ups, limits with windows as one side's consecutive substitutions in play, the points override at
  `scoring.points.<EVENT_TYPE>`, the rule list). The checks are in `Engines/TimelineChecks` (sport-engine.md, "As built
  (S05-T2b1)"). Scenario files gain `correction_window_open` and an event `source` (`BACKFILL`, `ADMIN`); the runner
  records a correction in the period of the event it replaces. For later: football's `VAR_DECISION` must carry
  `reverses` when its outcome is `DECISION_OVERTURNED` (a comment in the file, a payload meaning generic code can't
  read; S12 decides); rugby's `TEMPORARY_REPLACEMENT_END` names its returning player as `PLAYER_ON`, drawn from the
  `BENCH`, which the pool check refuses (S15 fixes the file or the rule); reversing a scoring event doesn't release
  a follow-up that consumed it (T2b2 notes it).
  Review #79 (owner, 2026-10-30: the defaults): after `ENDED` the open window takes only corrections and reversals, and
  never a reversal that would bring back or undo a period's start or end; Q28–Q31 (open-questions.md) keep the defaults
  the code follows.

