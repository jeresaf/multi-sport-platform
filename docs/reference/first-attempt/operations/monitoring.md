# Operational monitoring

The staging monitoring stack is defined in
`infrastructure/deployment/staging/docker-compose.staging.yml` and includes:

- Prometheus at `http://localhost:9090`
- Alertmanager at `http://localhost:9093`
- Grafana at `http://localhost:3002`
- alert email delivery into Mailpit at `http://localhost:8025`

Grafana provisions the **SportsPlatform Operations** dashboard automatically.
Its tenant selector filters tenant-aware queue, job, notification, usage, and
request metrics. Prometheus scrapes the API's `/metrics` endpoint every 15
seconds and loads `infrastructure/monitoring/prometheus/alerts.yml`.

## Metric contract

| Requirement | Metric |
|---|---|
| API health | `sportsplatform_api_up` and Prometheus `up` |
| Database/Redis health | `sportsplatform_dependency_health` |
| Active SignalR connections | `sportsplatform_signalr_connections` |
| Queue backlog | `sportsplatform_queue_backlog` |
| Failed jobs | `sportsplatform_failed_jobs` |
| Notification failures | `sportsplatform_notification_failures` |
| Tenant usage | `sportsplatform_tenant_usage` |
| Error rate | `sportsplatform_http_requests_total` |

Tenant identifiers are labels only on operational series where tenant-level
triage is useful. Secrets, user identifiers, emails, request bodies, and raw
URLs are never metric labels.

## Alert response

### API or metrics down

Check `docker compose ... ps api`, API logs, `/health`, then the Prometheus
target page. Roll back the API image if health stopped after a deployment.

### Database unhealthy

Check the `db` container and PostgreSQL readiness before restarting the API.
Do not remove the database volume. Use the tested restore procedure if the
database cannot recover normally.

### High API error rate

Use Grafana's error-rate panel and API structured logs to identify the failing
operation and tenant context. Avoid retrying non-idempotent writes manually.

### Redis unhealthy

Confirm PostgreSQL-backed operations remain available, inspect Redis logs and
storage, then restart only Redis if safe. Redis is an optional health dependency.

### Queue backlog

Use the dashboard tenant split, inspect pending/processing reports, and confirm
the report worker is running. Reprocess only through the permissioned support
workflow.

### Failed jobs

Inspect the failed report and its audit record before retrying. Preserve the
failure reason and tenant boundary.

### Notification failures

Confirm provider credentials and connectivity, inspect failed durable records,
then retry through the notification workflow. Never copy device tokens into
logs or metric labels.

## Configuration validation

Before deployment, run:

```powershell
docker compose -f infrastructure/deployment/staging/docker-compose.staging.yml `
  --env-file infrastructure/deployment/staging/.env.staging config
```

After startup, verify Prometheus `/api/v1/targets`, `/api/v1/rules`, Grafana
`/api/health`, Alertmanager `/-/healthy`, and that a test alert reaches Mailpit.
