param(
    [string]$ContainerName = 'staging-db-1',
    [string]$Database = 'sports_platform_staging',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'backups\staging')
)

$ErrorActionPreference = 'Stop'
if ($ContainerName -notmatch '^[a-zA-Z0-9_.-]+$' -or
    $Database -notmatch '^[a-zA-Z0-9_]+$') {
    throw 'Container and database names contain unsupported characters.'
}
if (-not (docker ps --format '{{.Names}}' | Where-Object { $_ -eq $ContainerName })) {
    throw "PostgreSQL container '$ContainerName' is not running."
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null
$timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')
$fileName = "${Database}_${timestamp}.dump"
$backupPath = Join-Path $resolvedOutput $fileName
$containerPath = "/tmp/${fileName}"

try {
    & docker exec $ContainerName pg_dump `
        -U postgres `
        -d $Database `
        -F c `
        -f $containerPath
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed.' }
    & docker cp "${ContainerName}:${containerPath}" $backupPath
    if ($LASTEXITCODE -ne 0) { throw 'docker cp failed.' }
}
finally {
    & docker exec $ContainerName rm -f $containerPath 2>$null
}

$file = Get-Item -LiteralPath $backupPath
if ($file.Length -le 0) { throw 'The created backup is empty.' }
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $backupPath
$manifest = [ordered]@{
    createdAtUtc = [DateTime]::UtcNow.ToString('o')
    container = $ContainerName
    database = $Database
    file = $file.Name
    bytes = $file.Length
    sha256 = $hash.Hash
}
$manifestPath = "$backupPath.manifest.json"
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Output $backupPath
