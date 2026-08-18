# Public CSV staging seed

The staging deployment imports the source pack at
`../docs/public_multi_sport_seed_csv_pack` into the `uganda-sports-demo` tenant.
The importer validates all 19 files, uses stable GUIDs, runs inside a database
transaction, and skips an already-complete import.

## Full reset

From the `sports-platform` directory in PowerShell:

```powershell
docker compose --env-file infrastructure/deployment/staging/.env.staging `
  -f infrastructure/deployment/staging/docker-compose.staging.yml `
  down --volumes --remove-orphans

docker compose --env-file infrastructure/deployment/staging/.env.staging `
  -f infrastructure/deployment/staging/docker-compose.staging.yml `
  up -d --build
```

This is destructive and removes all staging PostgreSQL, Redis, MinIO, Mailpit,
monitoring, backup, and data-protection volumes. It must not be used for production.
The API mounts the CSV directory read-only at `/app/seed-data`.

Current public web: `http://192.168.1.68:3001`

Current admin web: `http://192.168.1.68:3000`

Current API: `http://192.168.1.68:8080`

The Wi-Fi address is assigned by the router and can change. Confirm it with
`Get-NetIPAddress -AddressFamily IPv4` before device testing.

## Domain mappings

- Rugby 7s and Rugby 15s share the backend `RUGBY` sport and use distinct ruleset variants.
- `STARTING_SOON` maps to `Ready`.
- `POSTPONED` maps to `Scheduled`, because contest postponement is not yet a backend status.
- `APPROVED` maps to a published contest/result so it is visible through public APIs.
- CSV `MEET`, `SERIES`, and `PLACEMENT` stages map to the nearest implemented stage types.
- CSV series are stored through the contest series fields (`SeriesId`, game number, best-of).
- Venue records map to the contest venue string; the current schema has no venue aggregate.
- Presentation metadata drives the chosen contest shape/ruleset. The current schema has no
  competition-level presentation-metadata entity, so view lists are not stored verbatim.

The source pack's fixed anchor is `2026-08-13` in `Africa/Kampala`.
