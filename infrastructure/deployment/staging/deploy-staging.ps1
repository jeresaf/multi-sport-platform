param(
    [ValidateSet('start', 'stop', 'restart', 'logs', 'status', 'build', 'clean', 'validate')]
    [string]$Action = 'start'
)

$ErrorActionPreference = 'Stop'
$env:COMPOSE_PARALLEL_LIMIT = '1'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

$environmentFile = Join-Path $scriptDirectory '.env.staging'
$composeFile = Join-Path $scriptDirectory 'docker-compose.staging.yml'
$composeArguments = @('compose', '-f', $composeFile)

if (Test-Path -LiteralPath $environmentFile) {
    $composeArguments += @('--env-file', $environmentFile)
}
elseif ($Action -in @('start', 'build')) {
    throw "$environmentFile is required for the '$Action' action."
}

function Invoke-Compose {
    & docker @composeArguments @args
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed with exit code $LASTEXITCODE."
    }
}

function Test-Endpoint([string]$Url) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 5
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 400
    }
    catch {
        return $false
    }
}

function Assert-StagingConfiguration {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        if ($line -match '^\s*([A-Z][A-Z0-9_]*)=(.*)$') {
            $values[$Matches[1]] = $Matches[2].Trim().Trim('"').Trim("'")
        }
    }

    foreach ($name in @('DB_PASSWORD', 'JWT_SECRET')) {
        if (-not $values.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($values[$name])) {
            throw "$name is required in $environmentFile. Copy .env.example and provision a secret value."
        }
    }

    if ($values['DB_PASSWORD'].Length -lt 12) {
        throw "DB_PASSWORD in $environmentFile must contain at least 12 characters."
    }
    if ([Text.Encoding]::UTF8.GetByteCount($values['JWT_SECRET']) -lt 32) {
        throw "JWT_SECRET in $environmentFile must contain at least 32 UTF-8 bytes."
    }
    if ($values['JWT_SECRET'] -match '(?i)change-me|developmentonly|example|replace-|yoursupersecret') {
        Write-Warning "JWT_SECRET in $environmentFile appears to contain an old example value. Rotate it before exposing staging outside the local machine."
    }

    foreach ($recommended in @('MINIO_ROOT_PASSWORD', 'GRAFANA_ADMIN_PASSWORD')) {
        if (-not $values.ContainsKey($recommended) -or [string]::IsNullOrWhiteSpace($values[$recommended])) {
            Write-Warning "$recommended is not set; staging will temporarily fall back to DB_PASSWORD. Provision a distinct value at the next credential rotation."
        }
    }
}

if ($Action -in @('start', 'build', 'validate')) {
    Assert-StagingConfiguration
}

switch ($Action) {
    'start' {
        # Build and recreate serially. Docker Desktop on Windows can otherwise
        # race while renaming parallel replacement containers.
        Invoke-Compose build
        Invoke-Compose up -d
        # Nginx resolves Compose service names when it starts. Refresh it after
        # application containers are replaced so it cannot retain stale IPs.
        Invoke-Compose restart nginx
        $healthy = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            if ((Test-Endpoint 'http://localhost:8080/health') -and
                (Test-Endpoint 'http://localhost/health') -and
                (Test-Endpoint 'http://localhost:3000/') -and
                (Test-Endpoint 'http://localhost:3001/') -and
                (Test-Endpoint 'http://localhost:9000/minio/health/live') -and
                (Test-Endpoint 'http://localhost:8025/readyz')) {
                $healthy = $true
                break
            }
            Start-Sleep -Seconds 2
        }
        Invoke-Compose ps
        if (-not $healthy) {
            Invoke-Compose logs --tail 100 api admin-web public-web
            throw 'One or more application services did not become healthy within 60 seconds.'
        }
        Write-Host 'API:        http://localhost:8080'
        Write-Host 'Admin web:  http://localhost:3000'
        Write-Host 'Public web: http://localhost:3001'
        Write-Host 'Object API: http://localhost:9000'
        Write-Host 'Object UI:  http://localhost:9001'
        Write-Host 'Mail UI:    http://localhost:8025'
    }
    'stop' { Invoke-Compose down }
    'restart' { Invoke-Compose restart }
    'logs' { Invoke-Compose logs -f }
    'status' { Invoke-Compose ps }
    'build' { Invoke-Compose build --no-cache }
    'validate' {
        Invoke-Compose config --quiet
        Write-Host 'Staging configuration is valid.'
    }
    'clean' {
        Write-Warning 'This removes the staging database and Redis volumes.'
        Invoke-Compose down -v
    }
}
