# UI cleanup implementation status

Source of truth: `../docs/sports-platform-ui-codex-task-list.md`.

## Implemented in the first UI cleanup slice

- UI-001–004: canonical JSON/CSS tokens, Flutter token projection, shared responsive
  breakpoints, component-oriented web styles, and `/ui-gallery`.
- UI-005–015: dynamic web sport navigation, public Live/Favourites routes, universal
  search entry points, date navigation, contest filters, competition sections, and
  responsive desktop/tablet/mobile match-centre layouts. Flutter now uses the same
  date-centric public home, five phone tabs, and an adaptive tablet rail with Search.
- UI-016–020 and UI-025/027/028/030: canonical status and participant components,
  renderer registry, score-aware head-to-head cards, multi-participant/race/combat/
  cricket mappings, and matching Flutter fixture view models.
- UI-041–043: fixtures expose competition and sport identity, renderer metadata,
  official participant results, and bounded date filtering. Web and Flutter map the
  contract into presentation models rather than binding screens to raw JSON.
- UI-021–024: shared web and Flutter head-to-head adapters now cover football,
  basketball, Rugby 7s/15s, netball, hockey, volleyball, and tennis. Cards consume
  backend score variants, stage/period/clock labels, official or live scores, rugby
  scoring-event summaries, series context, and set-score displays. Rugby uses one
  shared base with an explicit Sevens/Fifteens variant and never hard-codes duration.
- UI-031–040: competition detail now uses a reusable hero, backend-driven deep-link
  navigation, relevant overview modules, responsive full/compact standings, group and
  conference layouts, mobile-safe knockout rounds, series cards, leaderboards, and
  medal tables through matching web and Flutter output registries. Follow and alert
  choices persist locally. Standings render rugby bonus points, form chips,
  qualification indicators, and optional points deductions from API metadata.
- UI-044–047: accessible live indicators, versioned authoritative SignalR patches,
  reconnect snapshots, favourites for contests/competitions/teams/athletes, matching
  home-filter behavior, and per-favourite alert preferences. Alert topics are scoped by
  the five authoritative basic event types emitted by the current backend contract.
- UI-048–053: tenant colours, explicit light/dark
  theme, keyboard focus and skip navigation, responsive no-overflow checks, and
  loading/empty/error states.
- UI-055–057: `/live`, `/favourites`, `/sport/{slug}`, and singular competition route
  aliases; Flutter route semantics; persisted competition presentation settings with
  validated admin controls and a PostgreSQL migration.
- UI-058–060: component-gallery fixtures, the required seven-size Playwright
  screenshot matrix under `artifacts/ui-matrix/`, and committed Flutter public-home
  goldens for phone portrait/landscape, 8-inch and 10-inch tablet portrait, tablet
  landscape, empty, and API-error states.

## Follow-on UI slices

- Add richer football aggregate/penalty and basketball per-period breakdowns when
  those optional values are supplied by the scoring contract; the core UI-021–024
  adapters and current seeded scenarios are complete.
- Review the committed Flutter baselines on the Android test device and verify the iOS
  build on macOS. These are external device/platform checks rather than missing UI-060
  host coverage.

## Verification recorded 2026-08-13

- Public web: lint, TypeScript, and production build pass.
- Admin web: focused lint, TypeScript, and production build pass.
- API: warning-free build, migration drift check, two public-contract tests, and the
  presentation-settings workflow test pass.
- Flutter: `flutter analyze` and all 32 host tests pass.
- Docker: API, admin, public web, PostgreSQL, MinIO, and Mailpit are healthy; the new
  migration is applied; the CSV tenant still has exactly 18 competitions and 138
  contests; public web responds on localhost and `192.168.1.68:3001`.
- Browser: tenant-aware homepage and all seven required viewport screenshots pass
  without horizontal overflow.

## Verification recorded 2026-08-13 — sport adapters

- API fixture feed now returns live scores and a presentation object containing the
  competition score variant plus CSV-authored stage, period, clock, summary, series,
  and approved event-summary values.
- Existing CSV staging data is upgraded idempotently on startup; no volume reset was
  performed. Both `RUGBY_7` and `RUGBY_15` are present in the live Docker response.
- Public web TypeScript and lint pass; API builds warning-free and its focused public
  contract integration test passes; Flutter analysis and two focused rugby adapter
  tests pass.
- Docker API and public web are healthy. The public home and `/ui-gallery` return HTTP
  200 locally and the public home returns HTTP 200 at `192.168.1.68:3001`.

## Verification recorded 2026-08-13 — competition experience

- UI-031–040 are checked complete in the authoritative UI task list. Web and Flutter
  use mirrored output registries for standings, groups/conferences, brackets, series,
  leaderboards, and medals; irrelevant navigation remains controlled by the API.
- The existing CSV staging volume was preserved. API startup reconciled standings
  metadata idempotently: Uganda Rugby Premiership exposes six rows including bonus
  points and five-match form, while Kampala Sevens exposes 12 grouped rows.
- Public web lint, TypeScript, and production build pass. The API solution builds with
  zero warnings/errors and the focused public competition contract test passes.
  Flutter analysis and all 31 tests pass, including standings expansion and mobile
  bracket round selection.
- Rebuilt staging API and public-web containers are healthy; competition list and
  detail pages return HTTP 200, and the public API exposes Rugby sport/region plus the
  backend-controlled competition tabs.
- Mobile-width browser smoke checks pass without horizontal overflow for Rugby 15s
  bonus/form standings, Sevens groups, a tied best-of-three basketball series with
  per-game scores, and the athletics medal table derived from seeded finish ranks.

## Verification recorded 2026-08-13 — live updates and alerts

- UI-044–047 are checked complete in the authoritative task list. API score/status/
  segment/result broadcasts now include a versioned contest patch with authoritative
  participant totals; reconnect snapshots expose the same totals.
- Public web and Flutter subscribe to the visible contests and patch only the matching
  feed item. Stale versions are ignored, subscriptions recover after reconnect, and
  neither client reloads the whole daily feed for score events.
- Contest, competition, team, and athlete favourites are persisted and all can surface
  matching contests in the Favourites filter. The formerly mocked mobile participant
  page now consumes the public participant endpoint.
- Basic alert choices persist per favourite. Mobile subscribes to
  event-specific contest/competition push topics rather than receiving every favourite
  event; the tenant topic remains available for organisation-wide messages.
- Public web lint/type-check/production build, warning-free .NET build, focused SignalR
  integration and push-topic tests, Flutter analysis, and all 32 mobile tests pass.
  Rebuilt API/public-web containers are healthy. Docker browser verification shows one
  fixture request per page load, active SignalR negotiation, persisted alert choices,
  participant favourites, and no mobile-width overflow.

## Verification recorded 2026-08-13 — Flutter visual regression

- UI-060 is checked complete in the authoritative UI task list. Seven committed PNG
  baselines cover the required phone portrait/landscape, 8-inch and 10-inch tablet
  portrait, and tablet landscape matrix, plus public-home empty and API-error states.
- The matrix renders the production `HomeScreen` with deterministic local multi-sport
  fixtures, Flutter SDK Roboto and Material Icons, a fixed device-pixel ratio, public
  login access, sport filters, and live/upcoming/finished Rugby 7s/15s cards.
- Visual inspection found and fixed pale/white sport-filter labels by giving light and
  tenant-branded chip themes explicit accessible foreground colours.
- Baseline comparison passes without update mode, `flutter analyze` reports no issues,
  and the complete Flutter suite passes all 39 tests.

## Verification recorded 2026-08-13 — navigation and discovery

- UI-005–008 and UI-055–056 are checked complete in the authoritative task list.
  Web flagship sports are configuration/API-driven with route-aware navigation, and
  public search now groups Sports, Competitions, Teams, Athletes, Contests, and Venues.
- The API exposes active sports used by the tenant's published competitions/contests
  and returns canonical lowercase sport, competition, team, and athlete links.
- Web date navigation shows three, five, or seven days responsively and retains the
  selected date in `?date=YYYY-MM-DD`. Flutter provides three/five-day buttons, swipe,
  calendar selection, and matching route state.
- Flutter now uses an indexed stateful navigation shell, so tab stacks and scroll state
  survive tab changes. Canonical `/home`, `/sport/:slug`, `/competition/:id`,
  `/team/:id`, and `/athlete/:id` routes coexist with compatibility aliases.
- Public-web lint and production build pass; the focused public-search integration
  test passes; Flutter analysis and all 41 tests pass. Six intentionally changed home
  goldens were visually inspected and refreshed for the responsive date navigator.
- Rebuilt API/public-web Docker services are healthy without resetting staging data;
  public web returns HTTP 200 locally and at `192.168.1.68:3001`, and public search
  returns seven enabled sports with canonical lowercase links.

## Verification recorded 2026-08-13 — responsive golf leaderboard

- UI-026 is checked complete in the authoritative task list. The golf API now derives
  `todayToPar` and `thru` from the participant's scored holes and the configured hole
  pars, while retaining tied positions, round totals, cut, withdrawal, and
  disqualification status.
- Web uses one leaderboard component in the standalone page and canonical competition
  leaderboard tab: desktop shows R1–R4 plus Today/Thru/Total, tablet keeps the compact
  live columns, and mobile rows expand to round and stroke details.
- Flutter mirrors those responsive layouts and reuses the same leaderboard widget in
  competition detail and the full leaderboard screen. Cached, loading, empty, and
  retry behavior remains available in the full screen.
- Public-web lint and production build pass; the API builds with zero warnings and the
  four-round tied-cut integration workflow passes; Flutter analysis and all 43 tests
  pass, including new mobile expansion and tablet-column tests.
- Rebuilt API/public-web containers are healthy without changing the staging volume.
  The seeded Savannah Invitational returns four entries with Today/Thru fields, and
  both its canonical competition tab and LAN leaderboard page return HTTP 200.

## Verification recorded 2026-08-13 — grouped fight cards

- The authoritative checklist was reconciled with the implementation status evidence;
  verified UI-001–004, UI-009–025, UI-027–028, UI-030, UI-041–043, UI-048–053,
  UI-057–059 are now marked complete instead of remaining stale unchecked entries.
- UI-029 is checked complete. Web and Flutter group backend-authored combat bouts into
  Main Event, Co-Main Event, Main Card, and Preliminaries, retaining bout billing,
  weight class, rounds, fighters, officials, winner, decision, and stoppage time.
- Combat competition pages link to the full fight card. Flutter adds a public fight-card
  route, pull-to-refresh, loading/empty/retry handling, and cached low-data/offline use.
- Public-web lint and production build pass; API builds with zero warnings and the
  eight-bout combat integration workflow passes; Flutter analysis and all 45 tests pass,
  including section-order and offline-cache coverage.
- The deterministic public showcase now idempotently exposes all four billing sections
  on the existing staging volume. Rebuilt API/public-web containers are healthy, the
  combat endpoint returns Main Event, Co-Main Event, Main Card, and Preliminaries, and
  the LAN fight-card page returns HTTP 200.

## Verification recorded 2026-08-13 — feed performance

- UI-054 is checked complete, closing the internal UI checklist. Web daily feeds use a
  bounded per-tenant/date two-minute cache, deduplicate concurrent requests, ignore
  stale date responses, show content-shaped skeletons during uncached transitions,
  and keep cached entries synchronized with incremental SignalR patches.
- Web contest cards are memoized and filtered/grouped feed projections are memoized,
  preventing unrelated state and live score changes from re-rendering every card.
- Flutter retains its Drift-backed feed cache and sliver grids, preserves existing data
  during manual refresh, size-bounds the tenant logo decode, watches only feed structure
  at page level, and uses per-fixture providers so a score patch notifies only its card.
- Public-web lint and production build pass. Flutter analysis, targeted live-update and
  unchanged golden tests, and all 46 mobile tests pass; a new selector test proves an
  update to one fixture does not notify the neighbouring fixture selector.
- The optimized public-web container is healthy, and the showcase homepage returns
  HTTP 200 locally and at `192.168.1.68:3001`.
