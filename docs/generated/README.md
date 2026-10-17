# Generated catalogues

Files in this folder are written by the backend CLI (from S01) — never by hand:

| File | Source |
|---|---|
| `actions.md` | Action catalogue declared in each module (docs/architecture/authorization.md) |
| `errors.md` | Error codes declared in each module (docs/architecture/api-conventions.md) |
| `events.md` | Integration events declared in each module's Contracts (docs/architecture/events-and-jobs.md) |
| `entitlements.md` | Entitlement definitions (from S04) |

CI regenerates them and fails if the committed versions are stale. Machine-readable copies live in `/contracts`.
