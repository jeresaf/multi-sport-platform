# Empty-database administration walkthrough

The complete dependency-ordered acceptance catalogue is in
`docs/empty-database-ui-use-case-catalog.md`. Use that file as the source of
truth and this document only as the short environment/runbook summary.

This walkthrough uses an isolated Docker project and database. It does not
delete the diverse staging database. The database starts with platform sport
definitions, system roles, and exactly one user:

It also contains the reserved `Platform Administration` tenant used to scope
the bootstrap account. That reserved record is infrastructure, not walkthrough
business data.

- `admin@sportsplatform.com`
- `Admin123!`

Change that password if the environment is exposed beyond the local network.

## Start from empty on Windows 10

From the repository root:

```powershell
.\infrastructure\deployment\staging\start-walkthrough.ps1 -Reset
```

The script stops the normal staging containers without deleting their data,
then starts the isolated walkthrough environment on the usual ports. Open
`http://localhost:3000` and sign in with the bootstrap administrator.

Use the tenant slug `walkthrough-league` when creating the tenant. The public
web image is configured for that slug. If another slug is required, set
`WALKTHROUGH_TENANT_SLUG` in `.env.staging` before starting and rebuild.

## End-to-end sequence

1. In **Tenants**, create the tenant and complete its name, slug, timezone,
   branding, and active subscription details.
2. Switch into the new tenant, then create its users and assign least-privilege
   roles (tenant owner, competition administrator, fixture manager, scorer,
   result approver, and viewer as needed).
3. Create and publish a competition. Select the sport and ruleset, add an
   edition, then configure league, group, knockout, series, or leaderboard
   stages as appropriate.
4. Create teams or organizations under **Participants**.
5. Create athletes/players with sensible roster information.
6. Open each team and assign its players, including shirt number, position,
   and active dates where supported.
7. Create or generate fixtures, verify venues and start times, then move each
   fixture through its legal readiness states.
8. Open a fixture's scoring session. Start it, record play-by-play events and
   score changes, exercise corrections/reversals, and end the contest.
9. Submit and approve results. Check the admin result, standings, bracket or
   series, statistics, public web match centre, and mobile match centre.
10. Repeat scoring and approval until the competition is complete, verifying
    progression and standings after each round.
11. Test historical data twice: first with manually entered historical
    play-by-play; then with the supported import workflow. Verify idempotent
    re-import, error reporting, timelines, results, and derived tables.

Use different competitions to cover league, grouped league, single
elimination, best-of series, individual leaderboard, heats/qualification, and
multi-stage formats across all flagship sports.

## Resume the diverse seeded staging database

```powershell
.\infrastructure\deployment\staging\stop-walkthrough.ps1
```

This stops the walkthrough containers and restarts the existing staging
project. Its database volume is preserved.
