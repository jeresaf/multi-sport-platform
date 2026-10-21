# Generated catalogues

Files in this folder are written by the backend CLI (from S01) — never by hand:

| File | Source |
|---|---|
| `actions.md` | Action catalogue declared in each module (docs/architecture/authorization.md) |
| `errors.md` | Error codes declared in each module (docs/architecture/api-conventions.md) |
| `events.md` | Integration events declared in each module's Contracts (docs/architecture/events-and-jobs.md) |
| `entitlements.md` | Entitlements declared in Tenancy, with each plan's value and the actions that name them (docs/architecture/authorization.md) |

CI regenerates them and fails if the committed versions are stale. Machine-readable copies live in `/contracts`.

- `../../contracts/openapi.json` — OpenAPI 3.1 document, written by `dotnet run --project backend/tools/SportsPlatform.Cli -- export-openapi`.
- `web/packages/api-client/src/generated/schema.ts` — TypeScript types, written by `pnpm --filter @msp/api-client generate` (in `web/`).
- `errors.md` / `../../contracts/errors.json` — error catalogue, written by `export-catalogues`.
- The `Contracts` CI job regenerates all of these and fails if anything differs from what's committed.
