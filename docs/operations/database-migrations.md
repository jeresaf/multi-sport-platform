# Database migrations

PostgreSQL is the authoritative database provider. The EF Core migration
baseline `20260812003326_PlatformBaseline20260812` represents all 73 application
tables in the current model. API startup applies pending migrations before
seeding data.

## Existing staging volumes

Older staging volumes predate usable EF migration metadata. On the first
startup after this baseline, the API adopts an existing database only when it
is PostgreSQL, the core `Tenants` table exists, and migration history is absent
or empty. It runs the legacy idempotent compatibility operations once and then
records the baseline. Unknown non-empty migration histories fail closed. Once
adopted, normal startup uses EF migrations and does not rerun compatibility
schema operations.

Back up a preserved volume before its first baseline deployment. Follow
[`backup-recovery.md`](backup-recovery.md) for the Windows backup and restore
commands.

## Create a migration

Set a PostgreSQL design-time connection explicitly. The factory rejects a
missing connection or a non-PostgreSQL provider so a SQLite migration cannot
be generated accidentally.

```powershell
$env:ConnectionStrings__DefaultConnection = `
  'Host=localhost;Port=5434;Database=sports_platform_migrations;Username=postgres;Password=<local-password>'

dotnet tool restore
dotnet ef migrations add <MigrationName> `
  --project backend\Infrastructure\SportsPlatform.Infrastructure\SportsPlatform.Infrastructure.csproj `
  --startup-project apps\api\SportsPlatform.Api.csproj `
  --context SportsPlatformDbContext
```

Review both the generated migration and model snapshot. Every schema change
must have a migration.

## Verify before deployment

```powershell
dotnet build apps\api\SportsPlatform.Api.csproj --disable-build-servers -m:1
dotnet test tests\IntegrationTests\SportsPlatform.IntegrationTests.csproj `
  --filter FullyQualifiedName~DatabaseMigrationBaselineTests `
  --disable-build-servers -m:1

dotnet ef migrations has-pending-model-changes `
  --project backend\Infrastructure\SportsPlatform.Infrastructure\SportsPlatform.Infrastructure.csproj `
  --startup-project apps\api\SportsPlatform.Api.csproj `
  --context SportsPlatformDbContext
```

Run the migration against a disposable PostgreSQL database before rebuilding
staging. After deployment, require a healthy readiness endpoint and confirm
that `__EFMigrationsHistory` contains the expected migration.

## Rollback

Prefer a forward corrective migration. If a deployment must be rolled back,
stop the API, restore the pre-deployment database backup, deploy the matching
previous API image, and then restart traffic. Do not delete migration-history
rows or manually reverse schema statements on a preserved database.
