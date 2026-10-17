# Provider Integration & External IDs System — Full Specification

## 1. Purpose

Normalize third-party sports data into canonical platform entities while preserving provider identifiers and preventing provider-specific schemas from leaking into the core platform.

---

# 2. Scope

In scope:
- Provider registry
- Provider credentials references
- External ID mapping
- Entity reconciliation
- Fixtures/status/timeline/stat ingestion adapters
- Raw payload retention policy
- Provider coverage mapping
- Sync jobs
- Conflict handling
- Provider priority/fallback policy

Out of scope:
- Public UI provider strings
- Provider-specific domain ownership
- Billing contracts with provider

---

# 3. Core Model

```text
Provider
ProviderConnection
ExternalIdentifier
ProviderEntityMapping
ProviderSyncRun
ProviderRawPayload(optional)
ProviderConflict
ProviderCoverageMap
```

External ID:

```text
entity_type
entity_id
provider
provider_id
```

---

# 4. Canonical Adapter Flow

```text
Provider Payload
    ↓
Provider Adapter
    ↓
Canonical Validation
    ↓
Entity Mapping
    ↓
Domain Command/Event
```

Examples:

```text
Provider status -> canonical ContestStatus
Provider period -> canonical sport phase
Provider event -> canonical TimelineEvent
Provider team/player ID -> ExternalIdentifier
```

---

# 5. Business Rules

## RULE-PRV-001
Provider enums never leak to public/web/mobile contracts.

## RULE-PRV-002
Provider IDs never replace internal canonical IDs.

## RULE-PRV-003
Unknown provider values must be logged/diagnosable.

## RULE-PRV-004
Provider correction must use canonical correction/reversal workflows.

## RULE-PRV-005
Mapping conflicts require explicit resolution when automatic match unsafe.

## RULE-PRV-006
Raw payload retention must follow privacy/storage policy.

---

# 6. Commands

```text
provider.register
provider.connect
provider.sync
provider.mapping.resolve
provider.conflict.resolve
provider.replay_payload
```

---

# 7. Queries

```text
Get provider mappings
Get sync status
Get conflicts
Get raw payload diagnostics
Get provider coverage
```

---

# 8. UI

Provider Connections  
Sync Status  
Entity Mapping  
Conflict Resolution  
Coverage Matrix  
Diagnostics

---

# 9. Errors

```text
PROVIDER_UNAVAILABLE
PROVIDER_MAPPING_MISSING
PROVIDER_MAPPING_CONFLICT
PROVIDER_PAYLOAD_INVALID
PROVIDER_STATUS_UNKNOWN
PROVIDER_RATE_LIMITED
```

---

# 10. Tests

- external IDs
- status normalization
- timeline normalization
- correction
- mapping conflict
- provider outage
- idempotent replay

---

# 11. Definition of Done

Complete when providers can be added/switched without changing canonical web/mobile contracts or domain identity.
