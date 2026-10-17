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
- Each module declares its error codes in code (`public static readonly Error` fields of a static `<Module>Errors`
  class); `export-catalogues` writes `docs/generated/errors.md` and `contracts/errors.json` (CI check).
- HTTP mapping (`ErrorKind` → status): 400 malformed/validation · 401 unauthenticated · 403
  permission/entitlement/access mode · 404 not found (also for other tenants' IDs) · 405 method not allowed ·
  409 state or concurrency conflict · 415 body is not JSON · 422 semantically invalid domain request · 429
  rate/quota · 500 unexpected · 503 dependency unavailable.
- Endpoints turn failures into Problem Details with `result.ToHttpResult(...)` / `error.ToProblem()`
  (`SportsPlatform.BuildingBlocks.Api`); the Host adds `type` and `correlationId`. `title` is the error's
  default message.
- Failures raised by the HTTP pipeline itself (`CommonErrors`): `VALIDATION_MALFORMED_REQUEST` (400, body or
  parameters unreadable), `VALIDATION_REQUEST_TOO_LARGE` (413, body over the size limit),
  `VALIDATION_UNSUPPORTED_MEDIA_TYPE` (415), `PLATFORM_ROUTE_NOT_FOUND` (404, no endpoint matched),
  `PLATFORM_METHOD_NOT_ALLOWED` (405), `PLATFORM_UNEXPECTED_ERROR` (500, any unhandled exception). Kestrel rejects
  408 (request timeout) and 431 (headers too large) before the pipeline runs, so those responses have no body.
- Never leak stack traces or internal details; they go to logs with the correlation ID.

## Correlation ID

- Every response carries `X-Correlation-Id`. An incoming value is kept if it is 1–64 characters of
  `[A-Za-z0-9._-]`; otherwise the API generates a UUIDv7. The same value is the Problem Details
  `correlationId` (the only request ID in the body; ASP.NET Core's `traceId` is removed) and is in the logging
  scope (`CorrelationId`).

## Logging, tracing and health

- Logs are Serilog JSON on the console, one object per line (`@t`, `@m`, `@l`, `@x` and properties). Every event
  logged during a request carries `CorrelationId`, `ActorKind`, `ActorId` and, once the request has a tenant,
  `TenantId`. ASP.NET Core's `RequestId` is removed, so `CorrelationId` is the only request ID in logs. One
  completion event per request (method, path without the query string, status, elapsed): Error for
  5xx and exceptions, Verbose for health probes, otherwise Information. Levels: `Serilog:MinimumLevel`.
- Never log `PRIVATE` or `SENSITIVE` values (ADR-0015); use message templates with IDs.
- OpenTelemetry traces and metrics for ASP.NET Core, HttpClient and Npgsql (SQL text, never parameter values;
  query-string values are redacted). Health probes are not traced. OTLP export only when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set; Azure Monitor gets its own exporter adapter (ADR-0016).
- `GET /health/live` → `200 {"status":"ok"}` while the process runs. `GET /health/ready` → `200 {"status":"ok"}`
  when the database answers on the `App` connection, else `503 {"status":"unavailable"}`. Health bodies never
  contain versions, host names or error text; the reason is logged as a Warning.

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
