# API Contract Standards

## 1. API Families

```text
/api/public/v1/...      Public read APIs
/api/admin/v1/...       Tenant/admin operations
/api/platform/v1/...    Platform-owner operations
/api/internal/v1/...    Internal application/service contracts if HTTP is ever required
```

Inside the modular monolith, prefer in-process application contracts over self-HTTP calls.

## 2. Resource Rules

- URLs use plural nouns.
- State changes with business meaning use explicit action endpoints where PATCH would hide workflow intent.
- Never expose persistence entities directly.
- Every tenant-scoped request derives tenant context from trusted authentication/domain context, not only request body.

Examples:

```text
POST /api/admin/v1/competitions
POST /api/admin/v1/editions/{id}/publish
POST /api/admin/v1/contests/{id}/start
POST /api/admin/v1/results/{id}/approve
```

## 3. IDs

Canonical IDs are opaque strings/UUID/ULID-style values selected by implementation standards.

Provider IDs appear only in external-identifier fields.

## 4. Pagination

Default cursor or stable page pagination contract:

```json
{
  "items": [],
  "page": {
    "nextCursor": null,
    "hasMore": false
  }
}
```

Use consistent filters/sorts across APIs.

## 5. Error Contract

```json
{
  "type": "https://errors.sportsplatform.local/CONTEST_NOT_READY",
  "code": "CONTEST_NOT_READY",
  "title": "Contest is not ready to start",
  "status": 409,
  "detail": "Required lineup approval is missing.",
  "correlationId": "...",
  "errors": []
}
```

Use RFC 7807/9457-style Problem Details semantics.

## 6. Concurrency

Mutable admin resources expose version/ETag or equivalent optimistic-concurrency token.

Conflict returns `409`.

## 7. Idempotency

Required for:
- live-scoring commands
- timeline creation
- import execution
- provider event ingestion
- externally retried commands

Header:

```text
Idempotency-Key
```

or canonical request field for offline devices.

## 8. Dates / Times

- API instants use ISO 8601 UTC offsets.
- Local display timezone comes from tenant/venue context.
- Date-only values are explicit date types.

## 9. Realtime

SignalR channels/groups should use stable scopes:

```text
tenant:{tenantId}
competition:{competitionId}
contest:{contestId}
participant:{participantId}
user:{userId}
```

Realtime messages include:
```text
type
entityId
version
occurredAt
payload
```

## 10. Versioning

Breaking public contract changes require API version strategy.
Internal event versions are independent from HTTP API versions.
