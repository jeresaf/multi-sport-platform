# Support tooling

Task 20.4 support operations are available in the admin web platform dashboard
and through a Windows-native PowerShell client. Both use the same authenticated
API and immutable audit ledger.

## Access

Only authenticated platform operators with `support.read` can inspect support
data. Mutations additionally require `support.manage`, an explicit confirmation,
and a non-empty reason. Startup provisioning grants these permissions to the
Admin, Platform Owner, and Platform Support system roles. Tenant roles and
scorers are denied.

The admin workspace is at `http://localhost:3000`. Platform operators can:

- search tenants by name, slug, or ID and inspect lease state;
- review failed durable report jobs and safely reprocess failed jobs only;
- review durable mobile scoring sequence conflicts;
- load score-event and correction history by contest ID;
- pause tenant notification delivery for four hours or resume it immediately;
- review the resulting support actions in recent audit history.

Notification pauses expire automatically. The API permits a maximum duration
of seven days. Pending notifications remain durable while paused and resume
normal worker delivery after the expiry or an explicit resume action.

## Windows PowerShell client

Do not place access tokens in command history. Set the token for the current
PowerShell process and run the client from the repository root:

```powershell
$env:SPORTS_PLATFORM_SUPPORT_TOKEN = "<current platform-support access token>"
.\infrastructure\scripts\support\support-operations.ps1 dashboard
.\infrastructure\scripts\support\support-operations.ps1 search-tenants -Search "school"
.\infrastructure\scripts\support\support-operations.ps1 failed-jobs
.\infrastructure\scripts\support\support-operations.ps1 sync-conflicts -TenantId <tenant-guid>
.\infrastructure\scripts\support\support-operations.ps1 contest-history -ContestId <contest-guid>
```

Mutation examples:

```powershell
.\infrastructure\scripts\support\support-operations.ps1 reprocess-job `
  -JobId <report-guid> -Reason "Failure reviewed; source data is now valid" -Confirmed

.\infrastructure\scripts\support\support-operations.ps1 pause-notifications `
  -TenantId <tenant-guid> -Hours 4 -Reason "Investigating duplicate delivery" -Confirmed

.\infrastructure\scripts\support\support-operations.ps1 resume-notifications `
  -TenantId <tenant-guid> -Reason "Delivery incident resolved" -Confirmed
```

PowerShell `-WhatIf` is supported for mutations. The API independently enforces
confirmation and reason requirements even if it is called without this script.
Clear the process token when finished:

```powershell
Remove-Item Env:SPORTS_PLATFORM_SUPPORT_TOKEN
```

## Audit actions

The append-only audit ledger records:

- `PlatformSupportActivatedTenant`
- `PlatformSupportSuspendedTenant`
- `PlatformSupportReprocessedReportJob`
- `PlatformSupportDisabledNotifications`
- `PlatformSupportEnabledNotifications`
- `LiveScoreSyncConflict`

Every support mutation records tenant, actor, target, before/after state, UTC
time, reason, IP address, and user agent. A sync conflict records operational
device and sequence context but does not store credentials or access tokens.

## Safety boundary

The reprocess operation accepts only reports currently in `Failed` state. It
does not retry arbitrary commands or jobs already pending/processing/completed.
Report generation is deterministic and tenant-scoped, making this the currently
approved safe reprocessing unit.
