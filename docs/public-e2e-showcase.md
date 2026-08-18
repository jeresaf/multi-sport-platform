# Public E2E showcase

The staging public site defaults to the deterministic tenant `public-e2e-showcase`.

- Public web: `http://192.168.1.67:3001`
- Public API: `http://192.168.1.67:3001/api/public/tenants/public-e2e-showcase`
- Mobile organisation code: `public-e2e-showcase`

The showcase contains nine published competitions across all seven supported
sports. Every competition has four public scenarios:

1. a completed, approved, published contest from 21 days ago;
2. an in-progress contest that started approximately 20 minutes before seeding;
3. a check-in-complete, ready contest starting in approximately two hours;
4. a scheduled contest seven days after seeding.

## Competition and format matrix

| Sport | Public competition | Format coverage |
|---|---|---|
| Football | East Africa Community Football League | League and standings |
| Football | County Champions Knockout Cup | Single-elimination bracket |
| Basketball | National Schools Basketball Championship | Groups and knockout |
| Golf | Savannah Invitational Golf Open | Multi-player leaderboard and hole scores |
| Athletics | National Track and Field Meet | Seeded 100m heats |
| Cricket | Kenya T20 Counties Championship | Group tournament and innings |
| Combat | Africa Fight Night | Fight card, three/five-round bouts, result methods |
| Rugby Union 15s | Great Rift Rugby Fifteens League | Fifteens timing and league standings |
| Rugby Sevens | Kenya Rugby Sevens Festival | Sevens timing, groups, and knockout |

All 36 showcase participants have human-readable fictional names. The showcase
also includes 20 standings rows, a public knockout bracket, published results,
and a scored golf round.

## Deeper completed demonstrations

The existing algorithm-heavy demos remain available when testing completed
tournament projections:

- `national-sports-demo`: football round robin and bracket, combat card,
  athletics qualification, T20 cricket, and Rugby Sevens.
- `school-games-demo`: basketball regular season and best-of-three playoffs.
- `short-tournament-demo`: four-round golf, cut line, ties, and leaderboard.
- `six-nations`: Rugby Union 15s round robin and bonus-point standings.

Pass the tenant with `?tenant=<slug>` on public-web routes, or enter the slug as
the organisation code in mobile.

## Suggested manual pass

Check the home page, competitions, schedule, results, standings, search,
competition detail, contest detail, participant detail, playoffs, golf
leaderboard, athletics, cricket, combat, and both rugby variants. Record the
route, competition/contest name, expected behavior, actual behavior, and a
screenshot for every pointed UI or workflow change.
