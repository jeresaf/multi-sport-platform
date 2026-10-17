# Reporting System — Full Specification

## 1. Purpose

Provide operational, competition, registry, audit, usage, and statistical reports without embedding report logic into transactional domains.

---

# 2. Report Categories

```text
Competition
Fixtures
Results
Standings
Registrations
Eligibility
Rosters
Teams
Participants
Officials
Venues
Statistics
Historical
Audit
Usage
Subscription/Quota
```

---

# 3. Dependencies

Consumes approved queries/read models from owning systems.

---

# 4. Core Model

```text
ReportDefinition
ReportRun
ReportParameterSet
ReportArtifact
ReportSchedule(optional)
```

Status:

```text
QUEUED
RUNNING
COMPLETED
FAILED
EXPIRED
```

---

# 5. Business Rules

## RULE-RPT-001
Reports must respect tenant scope and user permission.

## RULE-RPT-002
Private-field reports require explicit private-data permission.

## RULE-RPT-003
Large report generation should run asynchronously.

## RULE-RPT-004
Generated artifacts should have retention policy.

## RULE-RPT-005
Report definitions should not directly mutate domain data.

---

# 6. Formats

```text
CSV
XLSX
PDF
JSON (where appropriate/API)
```

---

# 7. Commands

```text
report.run
report.cancel
report.download
report.schedule
```

---

# 8. UI

Reports Hub  
Filters/Parameters  
Recent Reports  
Scheduled Reports  
Download History

---

# 9. Errors

```text
REPORT_NOT_FOUND
REPORT_PERMISSION_DENIED
REPORT_PARAMETER_INVALID
REPORT_GENERATION_FAILED
REPORT_ARTIFACT_EXPIRED
```

---

# 10. Tests

- tenant scoping
- large async report
- permission
- export formats
- artifact expiry
- historical report reproducibility

---

# 11. Definition of Done

Complete when common operational and analytical outputs can be generated without bypassing source-domain ownership.
