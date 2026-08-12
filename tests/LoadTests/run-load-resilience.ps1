param(
    [string]$ApiUrl = 'http://localhost:8080',
    [string]$EnvironmentFile = '',
    [string]$OutputDirectory = '',
    [switch]$FaultsOnly
)

$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Resolve-Path (Join-Path $scriptDirectory '..\..')
if ([string]::IsNullOrWhiteSpace($EnvironmentFile)) {
    $EnvironmentFile = Join-Path $repositoryRoot 'infrastructure\deployment\staging\.env.staging'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\load-resilience'
}
$composeFile = Join-Path $repositoryRoot 'infrastructure\deployment\staging\docker-compose.staging.yml'
$project = Join-Path $scriptDirectory 'SportsPlatform.LoadTests.csproj'
$resultsPath = Join-Path $OutputDirectory 'load-results.json'
$statePath = Join-Path $OutputDirectory 'load-state.json'
$faultsPath = Join-Path $OutputDirectory 'fault-results.json'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

if (-not (Test-Path -LiteralPath $EnvironmentFile)) {
    throw "Staging environment file not found: $EnvironmentFile"
}
$databasePasswordLine = Get-Content -LiteralPath $EnvironmentFile |
    Where-Object { $_ -match '^DB_PASSWORD=' } |
    Select-Object -First 1
if (-not $databasePasswordLine) { throw 'DB_PASSWORD is missing from the staging environment file.' }
$databasePassword = ($databasePasswordLine -split '=', 2)[1].Trim().Trim('"').Trim("'")
if ([string]::IsNullOrWhiteSpace($databasePassword)) { throw 'DB_PASSWORD is empty.' }

$compose = @('compose', '--env-file', $EnvironmentFile, '-f', $composeFile)
function Invoke-Compose([string[]]$Arguments) {
    & docker @compose @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed: $($Arguments -join ' ')" }
}
function Get-Endpoint([string]$Url, [int]$TimeoutSeconds = 5) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec $TimeoutSeconds
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Body = [string]$response.Content }
    }
    catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        return [pscustomobject]@{ Status = $status; Body = $_.Exception.Message }
    }
}
function Wait-Health([int]$ExpectedStatus, [int]$Attempts = 30) {
    for ($attempt = 0; $attempt -lt $Attempts; $attempt++) {
        $probe = Get-Endpoint "$ApiUrl/health"
        if ($probe.Status -eq $ExpectedStatus) { return $probe }
        Start-Sleep -Seconds 2
    }
    throw "API health did not reach HTTP $ExpectedStatus."
}
function Invoke-Runner([string]$Mode) {
    $env:LOAD_TEST_API_URL = $ApiUrl
    $env:LOAD_TEST_DB_PASSWORD = $databasePassword
    & dotnet run --project $project --configuration Release --no-restore -- `
        $Mode --output $resultsPath --state $statePath
    if ($LASTEXITCODE -ne 0) { throw "Load runner '$Mode' failed." }
}

$redisStopped = $false
$databaseStopped = $false
try {
    Wait-Health 200 | Out-Null
    if (-not $FaultsOnly) {
        Invoke-Runner 'run'
    }
    elseif (-not (Test-Path -LiteralPath $statePath) -or
            -not (Test-Path -LiteralPath $resultsPath)) {
        throw 'FaultsOnly requires existing successful load result and state artifacts.'
    }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($FaultsOnly) {
        $existingResults = Get-Content -LiteralPath $resultsPath -Raw | ConvertFrom-Json
        if (-not $existingResults.passed) {
            throw 'FaultsOnly requires a passing load result artifact.'
        }
    }

    Invoke-Compose @('stop', 'redis')
    $redisStopped = $true
    Start-Sleep -Seconds 2
    $redisHealth = Get-Endpoint "$ApiUrl/health"
    $redisDependencies = Get-Endpoint "$ApiUrl/health/dependencies" 12
    $redisPublic = Get-Endpoint "$ApiUrl/api/public/tenants/$($state.tenantSlug)/contests/$($state.publicContestId)"
    if ($redisHealth.Status -ne 200 -or
        $redisDependencies.Status -ne 200 -or
        $redisDependencies.Body -notmatch 'Degraded' -or
        $redisPublic.Status -ne 200) {
        throw 'The API did not remain available in degraded mode during the Redis outage.'
    }
    Invoke-Compose @('start', 'redis')
    $redisStopped = $false
    $redisRecovery = Wait-Health 200

    Invoke-Runner 'verify'
    Invoke-Compose @('stop', 'db')
    $databaseStopped = $true
    $databaseFailure = Wait-Health 503 20
    Invoke-Compose @('start', 'db')
    $databaseStopped = $false
    $databaseRecoveryWatch = [System.Diagnostics.Stopwatch]::StartNew()
    $databaseRecovery = Wait-Health 200 45
    $databaseRecoveryWatch.Stop()
    Invoke-Runner 'verify'

    $faultResults = [ordered]@{
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
        redisOutage = [ordered]@{
            healthStatus = $redisHealth.Status
            healthBody = $redisHealth.Body
            dependencyHealthStatus = $redisDependencies.Status
            dependencyHealthBody = $redisDependencies.Body
            publicContestStatus = $redisPublic.Status
            recoveryStatus = $redisRecovery.Status
            passed = $true
            behavior = 'Critical health remains HTTP 200; dependency health reports Degraded; PostgreSQL-backed public reads remain available.'
        }
        databaseRestart = [ordered]@{
            outageHealthStatus = $databaseFailure.Status
            recoveryHealthStatus = $databaseRecovery.Status
            recoverySeconds = [Math]::Round($databaseRecoveryWatch.Elapsed.TotalSeconds, 3)
            persistedStateVerified = $true
            passed = $true
            behavior = 'Health becomes HTTP 503 while PostgreSQL is unavailable and returns to HTTP 200 after restart; exact score and notification counts survive.'
        }
        passed = $true
    }
    $faultResults | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $faultsPath -Encoding utf8
    Write-Host "Load results:  $resultsPath"
    Write-Host "Fault results: $faultsPath"
}
finally {
    if ($databaseStopped) { Invoke-Compose @('start', 'db') }
    if ($redisStopped) { Invoke-Compose @('start', 'redis') }
    Remove-Item Env:LOAD_TEST_DB_PASSWORD -ErrorAction SilentlyContinue
}
