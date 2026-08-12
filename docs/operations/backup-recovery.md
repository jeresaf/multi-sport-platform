# Backup and recovery

## Automated staging backups

The staging Compose stack runs `db-backup`, which creates a PostgreSQL custom
format dump immediately at startup and every 24 hours thereafter. Dumps are
stored in the named `postgres_staging_backups` Docker volume and files older
than seven days are removed.

Inspect the current backup set on Windows:

```powershell
docker exec staging-db-backup-1 sh -c "ls -lh /backups"
```

## Manual Windows backup

Run from the `sports-platform` directory:

```powershell
.\infrastructure\database\backup.ps1
```

The script uses the running staging PostgreSQL container, writes a custom
format dump under `infrastructure\database\backups\staging`, and creates a JSON
manifest containing byte length and SHA-256 checksum. This local output is
ignored by source control.

## Safe restore

Restore into a disposable database by default:

```powershell
.\infrastructure\database\restore.ps1 `
  -BackupFile .\infrastructure\database\backups\staging\<backup>.dump
```

Restoring over `sports_platform_staging` is refused unless
`-ConfirmProtectedRestore` is supplied explicitly. Stop the API and take a new
backup before any protected restore.

Run the non-destructive recovery drill:

```powershell
.\infrastructure\database\test-restore.ps1
```

The drill creates a fresh backup, restores it into a uniquely named disposable
database, compares the tenant row count, and drops only that validated
disposable database.

## Object storage policy

MinIO data lives in the `minio_staging_data` Docker volume. For staging, take a
daily versioned mirror or volume snapshot and retain seven daily and four
weekly copies outside the Docker host. A recovery drill must restore the
snapshot to a separate MinIO volume and compare bucket/object inventory before
it can replace the active volume. Never edit files inside the MinIO data
volume directly.

Mailpit is test-only infrastructure. Its SQLite data persists in
`mailpit_staging_data`, but captured mail is not production business data and
is excluded from required recovery objectives.
