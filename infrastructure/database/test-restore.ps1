param(
    [string]$ContainerName = 'staging-db-1',
    [string]$SourceDatabase = 'sports_platform_staging'
)

$ErrorActionPreference = 'Stop'
$targetDatabase = "sports_platform_restore_verify_$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$backupScript = Join-Path $PSScriptRoot 'backup.ps1'
$restoreScript = Join-Path $PSScriptRoot 'restore.ps1'
$backupPath = & $backupScript `
    -ContainerName $ContainerName `
    -Database $SourceDatabase

try {
    & $restoreScript `
        -BackupFile $backupPath `
        -ContainerName $ContainerName `
        -TargetDatabase $targetDatabase `
        -ProtectedDatabase $SourceDatabase | Out-Null

    $countSql = 'SELECT COUNT(*) FROM "Tenants";'
    $sourceCount = (& docker exec $ContainerName psql `
        -U postgres -d $SourceDatabase -t -A -c $countSql).Trim()
    $restoredCount = (& docker exec $ContainerName psql `
        -U postgres -d $targetDatabase -t -A -c $countSql).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCount -ne $restoredCount) {
        throw "Restore verification failed: source=$sourceCount restored=$restoredCount."
    }
    Write-Output "Restore verified: $sourceCount tenant rows."
    Write-Output "Backup retained at: $backupPath"
}
finally {
    if ($targetDatabase -match '^sports_platform_restore_verify_[a-f0-9]{8}$') {
        $dropSql = "DROP DATABASE IF EXISTS `"$targetDatabase`" WITH (FORCE);"
        & docker exec $ContainerName psql `
            -v ON_ERROR_STOP=1 `
            -U postgres `
            -d postgres `
            -c $dropSql | Out-Null
    }
}
