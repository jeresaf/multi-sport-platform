# Platform Analytics System — Full Specification

## 1. Purpose

Measure platform/product usage and operational adoption without mixing product telemetry with sports statistics.

---

# 2. Scope

In scope:
- Feature usage
- Tenant activity
- Public traffic
- Admin activity
- Live-scoring sessions
- Import usage
- Report usage
- API usage
- Storage/volume metrics
- Adoption funnels
- Operational product telemetry

Out of scope:
- Player/team sport performance
- Audit truth
- Billing ledger
- Private PII analytics

---

# 3. Core Model

```text
AnalyticsEvent
AnalyticsMetric
AnalyticsAggregate
AnalyticsDimension
```

Examples:

```text
competition_created
contest_scored_live
report_generated
import_completed
tenant_activated
feature_denied_by_entitlement
```

---

# 4. Business Rules

## RULE-ANL-001
Do not place sensitive personal data in generic analytics payloads.

## RULE-ANL-002
Analytics events are not authorization/audit truth.

## RULE-ANL-003
Tenant-level metrics respect access controls.

## RULE-ANL-004
Metric definitions should be versioned/documented.

---

# 5. UI

Platform Analytics dashboard:
- Active tenants
- Competitions created
- Live contests
- Feature adoption
- Imports
- Reports
- Error/usage trends

Tenant analytics may be a limited subset.

---

# 6. Tests

- event capture
- tenant dimension
- sensitive-data exclusion
- aggregate correctness
- feature usage

---

# 7. Definition of Done

Complete when product decisions can be informed by reliable, privacy-safe usage telemetry distinct from audit and sports statistics.
