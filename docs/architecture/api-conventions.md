# API conventions

## Families

```text
/api/public/v1/...     public reads, tenant from host, no auth (rate-limited, cacheable)
/api/admin/v1/...      tenant administration and operations, authenticated, X-Tenant-Id
/api/platform/v1/...   platform staff operations (tenants, plans, diagnostics), authenticated
/hubs/live             SignalR
```

Modules call each other in-process through Contracts, never over HTTP.

## Resources and actions

- Plural nouns, kebab-case paths: `/api/admin/v1/competition-editions/{id}`.
- JSON properties camelCase; enums as UPPER_SNAKE strings.
- Use explicit action endpoints for workflow transitions instead of generic PATCH:
  `POST /api/admin/v1/contests/{id}/start`, `POST /api/admin/v1/results/{id}/approve`,
  `POST /api/admin/v1/fixtures/{id}/reschedule`.
- PATCH only for simple field edits on draft-like data; every transition with business meaning is a POST action.
- Every endpoint declares its action code (docs/architecture/authorization.md).

## Identifiers and slugs

- IDs are UUIDv7 strings. Public URLs may use tenant-unique slugs for competitions, editions (season label),
  teams, athletes and venues; APIs accept either where documented.
- Provider/federation IDs appear only in `externalIds` arrays.

## Errors (RFC 9457 Problem Details)

```json
{
  "type": "https://errors.example.com/CONTEST_NOT_READY",
  "title": "Contest is not ready to start",
  "status": 409,
  "detail": "Home lineup has not been approved.",
  "code": "CONTEST_NOT_READY",
  "correlationId": "01J…",
  "errors": [ { "field": "lineups.home", "code": "LINEUP_NOT_APPROVED" } ]
}
```

- The `type` base URI is configuration (`ProblemDetails:TypeBaseUri`); `example.com` above is a placeholder.
- `code` format `<AREA>_<CONDITION>`, stable forever; message text may change. UI branches on `code`.
- Area prefixes use readable names (the pack's three-letter table contradicted its own examples):
  `PLATFORM_ AUTH_ TENANT_ ENTITLEMENT_ SPORT_ FORMAT_ RULESET_ PERSON_ ORGANISATION_ TEAM_ VENUE_
  MEMBERSHIP_ STAFF_ TRANSFER_ COMPETITION_ EDITION_ REGISTRATION_ ROSTER_ ELIGIBILITY_ STAGE_ FIXTURE_
  CONTEST_ LINEUP_ OFFICIAL_ TIMELINE_ RESULT_ STANDINGS_ STATS_ SEARCH_ REPORT_ IMPORT_ PROVIDER_
  NOTIFICATION_ BRAND_ SPONSOR_ MEDIA_ AUDIT_ INTEGRITY_ VALIDATION_`.
- Each module declares its error codes in code; exported to `docs/generated/errors.md` (CI check).
- HTTP mapping: 400 malformed/validation · 401 unauthenticated · 403 permission/entitlement/access mode ·
  404 not found (also for other tenants' IDs) · 409 state or concurrency conflict · 422 semantically invalid
  domain request · 429 rate/quota · 500 unexpected · 503 dependency unavailable.
- Never leak stack traces or internal details; they go to logs with the correlation ID.

## Pagination, filtering, sorting

```json
{ "items": [ ], "page": { "nextCursor": "…", "hasMore": true } }
```

- Cursor pagination for large/growing lists (timeline, audit, people); `limit` default 25, max 100.
- Filters as query params with documented names (`status=LIVE&date=2027-03-14&sport=FOOTBALL_ASSOCIATION`).
- Sorting via `sort=-scheduledStart,name`.

## Concurrency

- Mutable admin resources return `version` (from PostgreSQL `xmin`) and `ETag`. Updates send `If-Match` or
  `version`; mismatch → `409 *_CONCURRENCY_CONFLICT`.

## Idempotency

- Required for: timeline recording (`clientEventId` in body), offline sync batches, import execution, provider
  ingestion, and any POST the client may retry (send `Idempotency-Key` header; stored per tenant + action for
  24 h; same key + different body → `409 PLATFORM_IDEMPOTENCY_CONFLICT`).

## Dates and times

- Instants: ISO 8601 with offset, stored UTC (`2027-03-14T15:00:00Z`). Date-only: `2027-03-14`.
- Responses include the relevant IANA time zone (`venueTimeZone`, `tenantTimeZone`) so clients render local
  times correctly. Never send local times without a zone.

## Versioning

- URL major version per family (`v1`). Additive changes don't bump the version. Breaking public changes need a
  new version and a deprecation window. Integration-event versions are independent.

## OpenAPI and clients

- The backend generates OpenAPI; the CLI writes `contracts/openapi.json` (committed so diffs are reviewable).
- Web and mobile clients are generated from it. A CI step fails if the committed document is stale.
- Every endpoint has a summary, request/response schemas, and documented error codes.
