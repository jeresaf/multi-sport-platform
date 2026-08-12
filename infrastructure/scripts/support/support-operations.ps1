[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet(
        "dashboard",
        "search-tenants",
        "failed-jobs",
        "sync-conflicts",
        "contest-history",
        "reprocess-job",
        "pause-notifications",
        "resume-notifications"
    )]
    [string]$Action,

    [string]$ApiUrl = "http://localhost:8080",
    [string]$Search,
    [Guid]$TenantId,
    [Guid]$ContestId,
    [Guid]$JobId,
    [string]$Reason,
    [ValidateRange(1, 168)]
    [int]$Hours = 4,
    [switch]$Confirmed
)

$ErrorActionPreference = "Stop"
$token = $env:SPORTS_PLATFORM_SUPPORT_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Set SPORTS_PLATFORM_SUPPORT_TOKEN to a current platform-support access token."
}

$baseUrl = $ApiUrl.TrimEnd("/")
$headers = @{ Authorization = "Bearer $token" }

function Invoke-SupportApi {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body
    )

    $parameters = @{
        Method = $Method
        Uri = "$baseUrl$Path"
        Headers = $headers
        TimeoutSec = 60
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 8
    }
    try {
        Invoke-RestMethod @parameters
    }
    catch {
        $detail = $_.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($detail)) { $detail = $_.Exception.Message }
        throw "Support API request failed: $detail"
    }
}

function Require-Guid {
    param([Guid]$Value, [string]$Name)
    if ($Value -eq [Guid]::Empty) { throw "$Name is required for action '$Action'." }
}

function Require-MutationApproval {
    if (-not $Confirmed) {
        throw "Mutation action '$Action' requires -Confirmed and a non-empty -Reason."
    }
    if ([string]::IsNullOrWhiteSpace($Reason)) {
        throw "A support reason is required for action '$Action'."
    }
}

$result = $null
switch ($Action) {
    "dashboard" {
        $result = Invoke-SupportApi -Method Get -Path "/api/platform-admin/dashboard"
    }
    "search-tenants" {
        if ([string]::IsNullOrWhiteSpace($Search)) { throw "Search is required." }
        $encoded = [Uri]::EscapeDataString($Search.Trim())
        $result = Invoke-SupportApi -Method Get -Path "/api/platform-admin/dashboard?search=$encoded"
        $result = $result.tenants
    }
    "failed-jobs" {
        $path = "/api/platform-admin/dashboard"
        if ($TenantId -ne [Guid]::Empty) { $path += "?tenantId=$TenantId" }
        $result = (Invoke-SupportApi -Method Get -Path $path).failedJobs
    }
    "sync-conflicts" {
        $path = "/api/platform-admin/dashboard"
        if ($TenantId -ne [Guid]::Empty) { $path += "?tenantId=$TenantId" }
        $result = (Invoke-SupportApi -Method Get -Path $path).syncConflicts
    }
    "contest-history" {
        Require-Guid -Value $ContestId -Name "ContestId"
        $result = (Invoke-SupportApi -Method Get -Path "/api/platform-admin/dashboard?historyContestId=$ContestId").eventHistory
    }
    "reprocess-job" {
        Require-Guid -Value $JobId -Name "JobId"
        Require-MutationApproval
        if ($PSCmdlet.ShouldProcess($JobId, "Reprocess failed durable report job")) {
            $body = @{ reason = $Reason.Trim(); confirmed = $true }
            $result = Invoke-SupportApi -Method Post -Path "/api/platform-admin/jobs/$JobId/reprocess" -Body $body
        }
    }
    "pause-notifications" {
        Require-Guid -Value $TenantId -Name "TenantId"
        Require-MutationApproval
        if ($PSCmdlet.ShouldProcess($TenantId, "Pause tenant notifications for $Hours hour(s)")) {
            $body = @{
                disabled = $true
                disabledUntilUtc = [DateTime]::UtcNow.AddHours($Hours).ToString("o")
                reason = $Reason.Trim()
                confirmed = $true
            }
            $result = Invoke-SupportApi -Method Post -Path "/api/platform-admin/tenants/$TenantId/notifications" -Body $body
        }
    }
    "resume-notifications" {
        Require-Guid -Value $TenantId -Name "TenantId"
        Require-MutationApproval
        if ($PSCmdlet.ShouldProcess($TenantId, "Resume tenant notifications")) {
            $body = @{
                disabled = $false
                disabledUntilUtc = $null
                reason = $Reason.Trim()
                confirmed = $true
            }
            $result = Invoke-SupportApi -Method Post -Path "/api/platform-admin/tenants/$TenantId/notifications" -Body $body
        }
    }
}

if ($null -ne $result) {
    $result | ConvertTo-Json -Depth 12
}
