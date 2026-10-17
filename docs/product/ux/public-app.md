# Public app (per-tenant site)

Decision: ADR-0010 (per tenant first). Stack: ADR-0011. The public app shows only approved public data, in the
tenant's branding, and updates live without page reloads.

## Principles

- **Date-centric and live-first.** Fans arrive asking "what's on / what happened today?"
- **Standings live inside competitions.** No global "Standings" menu — a table only makes sense in a
  competition context.
- **Deliberate layouts per form factor** — phone, tablet and desktop are designed, not just scaled:
  phone < 768 px, tablet 768–1199 px, desktop ≥ 1200 px.
- **Low-bandwidth friendly.** Many fans use mobile data on mid-range Android phones: server-render pages, keep
  client JavaScript small, serve resized images, lazy-load below the fold, avoid layout shift. Target Largest
  Contentful Paint under 2.5 s on a mid-range phone on 4G.
- **Honest data.** If a contest was recorded at `BASIC` coverage, don't show empty lineup or stats tabs — hide
  them or say "not recorded" (AR-30). Corrected results show a small "corrected" note with the date.
- **Accessible.** WCAG 2.2 AA; score changes announced politely to screen readers; never colour alone for
  status.

## Navigation

| Form factor | Pattern |
|---|---|
| Phone | Bottom navigation: Home · Live · Competitions · Favourites · Search |
| Tablet | Navigation rail + master/detail where helpful (list of contests left, detail right) |
| Desktop | Top bar (logo, Home, Live, sports, Competitions, Search) + left column of sports/competitions |

**Sports navigation:** the tenant's flagship sports (tenant setting) appear first; others under "More". A sport
with variants (Rugby → 15s / 7s) expands.

**Favourites in v1** are stored on the device (no account needed). Accounts with synced favourites and
notifications come with Engagement (S21).

## Home (date-centric)

```text
[ Sport chips: All · Football · Rugby · Basketball · Cricket · More ]
[ Date strip: … Thu 13 · Fri 14 (Today) · Sat 15 … 📅 ]
[ Filter: All · Live · Upcoming · Finished · Favourites ]

Football
  Metro Football League · Round 7
    [Contest card] [Contest card] …
Rugby
  Metro Sevens · Pool A
    [Contest card] …
```

- Past date → results; today → live first, then upcoming, then finished; future date → fixtures.
- Times shown in the venue time zone with a hint if it differs from the viewer's.
- Contest cards come from the contest card read model and update via SignalR (`tenant:{id}` group for the
  day's live contests).

## Contest card renderers

Chosen by `(shape, scoring_family)` (docs/architecture/sport-engine.md §10) and fed by `ScoreSummary`:

| Renderer | Used by | Shows |
|---|---|---|
| H2H · ADDITIVE | Football, rugby, basketball | Teams/badges, score, status badge, phase + clock (live), red-card markers, shoot-out score |
| H2H · INNINGS | Cricket | Each side's runs/wickets (overs), target/required rate when chasing, result text |
| H2H · SETS (later) | Tennis, volleyball | Sets and current game/points |
| H2H · COMBAT (later) | Boxing, MMA | Fighters, round, method of finish |
| FIELD · RANKED_METRIC (later) | Golf, athletics | Top positions, leader, round/heat |

States on every card: Not started (time), Starting soon, Live (pulsing dot + phase/clock), Half-time/Break,
Delayed, Interrupted, Suspended, Postponed, Cancelled, Abandoned, Ended (awaiting result), Final.

## Competition page

Header: competition + edition switcher (season), logo, sport, dates. Tabs depend on format and sport metadata:

| Format | Tabs |
|---|---|
| League | Overview · Fixtures · Results · Table · Statistics · Teams |
| Groups + knockout | Overview · Fixtures · Results · Groups · Knockout · Statistics · Teams |
| Knockout / playoffs | Overview · Fixtures · Results · Bracket · Teams |
| Sevens tournament (later format) | Overview · Schedule · Pools · Knockout · Results · Teams |
| Leaderboard (later) | Overview · Leaderboard · Rounds · Players |

Overview: next/live contests, latest results, table snippet, top scorers (when statistics exist).

## Match (contest) page

Hero: participants, score, status, phase and clock, venue and local kick-off time, competition/round link.
Tabs (shown only when data exists at the contest's coverage level):

- **Summary** — key events (goals/tries/cards/wickets) in order, with period markers.
- **Timeline** — all events with filters (Key / All / Scoring / Discipline / Substitutions); corrections shown as
  corrected; reversals shown struck through with the review decision.
- **Lineups** — starters, bench, staff; formation/positions where defined; substitutions and cards inline.
- **Stats** — team comparison and player box score (sport-specific layout from definition metadata).
- **Info** — officials (public names only), venue, attendance (if recorded).

Live behaviour: subscribe to `contest:{id}`; apply versioned deltas; on reconnect fetch events after the last
version.

## Team, player and venue pages

- **Team:** Overview (badge, motto, colours, organisation, home venue) · Fixtures · Results · Squad (current
  roster/members, public fields only) · Statistics · History (past editions, honours).
- **Player (athlete):** Overview (public profile) · Statistics · Matches · Teams (membership history).
  **Minors:** off by default; when enabled only display name, team and age group; photo only with consent
  (ADR-0015).
- **Venue:** location map link, capacity, upcoming contests.

## Screens checklist (v1 → later)

v1 (S10–S14): Home · Live · Competitions list · Competition (league tabs) · Contest page · Team · Player · Venue ·
Favourites (device) · Not found / tenant unavailable pages.
Later: Search (S20) · Accounts, follows, notification settings (S21) · Groups/Bracket/Series (S15–S17) ·
Leaderboard/Medals (Phase 9) · Sponsor placements (S23) · Branding (S22).
