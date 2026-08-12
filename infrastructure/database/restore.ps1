param(
    [Parameter(Mandatory)]
    [string]$BackupFile,
    [string]$ContainerName = 'staging-db-1',
    [string]$TargetDatabase = 'sports_platform_restore_verify',
    [string]$ProtectedDatabase = 'sports_platform_staging',
    [switch]$ConfirmProtectedRestore
)

$ErrorActionPreference = 'Stop'
$resolvedBackup = (Resolve-Path -LiteralPath $BackupFile).Path
if ($ContainerName -notmatch '^[a-zA-Z0-9_.-]+$' -or
    $TargetDatabase -notmatch '^[a-zA-Z0-9_]+$') {
    throw 'Container and database names contain unsupported characters.'
}
if ($TargetDatabase -eq $ProtectedDatabase -and -not $ConfirmProtectedRestore) {
    throw "Restoring over '$ProtectedDatabase' requires -ConfirmProtectedRestore."
}
if (-not (docker ps --format '{{.Names}}' | Where-Object { $_ -eq $ContainerName })) {
    throw "PostgreSQL container '$ContainerName' is not running."
}

$containerPath = "/tmp/restore_$([Guid]::NewGuid().ToString('N')).dump"
try {
    & docker cp $resolvedBackup "${ContainerName}:${containerPath}"
    if ($LASTEXITCODE -ne 0) { throw 'docker cp failed.' }

    $dropSql = "DROP DATABASE IF EXISTS `"$TargetDatabase`" WITH (FORCE);"
    & docker exec $ContainerName psql -v ON_ERROR_STOP=1 -U postgres -d postgres -c $dropSql
    if ($LASTEXITCODE -ne 0) { throw 'Dropping the exact target database failed.' }
    $createSql = "CREATE DATABASE `"$TargetDatabase`";"
    & docker exec $ContainerName psql -v ON_ERROR_STOP=1 -U postgres -d postgres -c $createSql
    if ($LASTEXITCODE -ne 0) { throw 'Creating the restore target failed.' }
    & docker exec $ContainerName pg_restore `
        -v `
        --exit-on-error `
        -U postgres `
        -d $TargetDatabase `
        $containerPath
    if ($LASTEXITCODE -ne 0) { throw 'pg_restore failed.' }
}
finally {
    & docker exec $ContainerName rm -f $containerPath 2>$null
}

Write-Output $TargetDatabase
