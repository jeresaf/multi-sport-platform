# Observability operations

The API emits one Serilog completion event for each request as structured JSON.
Unhandled exceptions produce one additional structured error event through
`IErrorReporter`. The default `LoggingErrorReporter` is safe to replace with a
vendor integration without changing HTTP error handling.

## Request and trace context

Clients may send `X-Correlation-Id` using up to 128 ASCII letters, digits,
hyphens, underscores, or periods. Invalid or absent values are replaced with the
current OpenTelemetry trace ID. Every response includes `X-Correlation-Id` and
the server-generated `X-Request-Id`.

ASP.NET Core OpenTelemetry instrumentation carries the trace across controllers
and application modules. Logs created during request handling include these
properties when available:

- `CorrelationId`, `RequestId`, and `TraceId`
- `TenantId` after tenant resolution
- `UserId` after authentication

Do not place email addresses, access tokens, request bodies, or other sensitive
data in correlation IDs or structured log properties.

## Health probes

| Route | Purpose | Checks |
|---|---|---|
| `/health` | Backward-compatible liveness alias | Process self-check |
| `/health/live` | Container liveness | Process self-check |
| `/health/ready` | Traffic readiness | PostgreSQL and Redis |
| `/health/dependencies` | Backward-compatible readiness alias | PostgreSQL and Redis |

All routes return JSON with the aggregate status, duration, and individual
check status. PostgreSQL failure makes readiness unhealthy. Redis is a degraded
dependency because authoritative operations remain PostgreSQL-backed; its
outage is visible in readiness output without forcing an API restart loop.

Use `/health/live` for Docker or orchestrator liveness. Use `/health/ready` for
traffic admission and deployment verification. Alert on an unhealthy PostgreSQL
check and on sustained Redis degradation.

## Error reporting integration

`IErrorReporter.ReportAsync` receives the exception plus correlation, request,
trace, tenant, user, method, and path context. The public production response
contains only a generic message, correlation ID, and trace ID. Register a
replacement `IErrorReporter` to connect an external service; the integration
must redact sensitive data and must not include credentials or request bodies.
