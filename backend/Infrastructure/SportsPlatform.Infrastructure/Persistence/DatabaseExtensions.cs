using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Subscriptions.Entities;

namespace SportsPlatform.Infrastructure.Persistence;

public static class DatabaseExtensions
{
    /// <summary>
    /// Applies pending migrations and seeds the database on startup.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<SportsPlatformDbContext>>();

        try
        {
            var context = services.GetRequiredService<SportsPlatformDbContext>();

            // Apply pending migrations
            if (context.Database.IsRelational())
            {
                var migrations = context.Database.GetMigrations().ToArray();
                if (migrations.Length == 0)
                {
                    throw new InvalidOperationException(
                        "No EF Core migrations were discovered. Relational database startup requires a migration baseline.");
                }

                var requiresBaselineAdoption = await RequiresMigrationBaselineAdoptionAsync(
                    context,
                    migrations,
                    logger);
                if (requiresBaselineAdoption)
                {
                    await ApplyLegacySchemaCompatibilityAsync(context);
                    await RecordMigrationBaselineAdoptionAsync(context, migrations, logger);
                }
                var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToArray();
                if (pendingMigrations.Length > 0)
                {
                    logger.LogInformation("Applying {Count} pending migrations...", pendingMigrations.Length);
                    await context.Database.MigrateAsync();
                    logger.LogInformation("Migrations applied successfully.");
                }
                else
                {
                    logger.LogInformation("No pending migrations found.");
                }
            }
            else
            {
                // For non-relational databases, ensure created
                await context.Database.EnsureCreatedAsync();
            }

            await EnsureAuditAppendOnlyAsync(context);

            var enableDemoData = app.Configuration.GetValue<bool>("SeedData:EnableDemoData");
            var seedMode = app.Configuration["SeedData:Mode"]?.Trim();
            var seedAdminOnly = string.Equals(
                seedMode, "AdminOnly", StringComparison.OrdinalIgnoreCase);
            var seedDemoData = enableDemoData && !seedAdminOnly;
            if (seedAdminOnly)
            {
                logger.LogInformation("Initializing the empty walkthrough database with platform reference data and the administrator account only.");
                await SeedData.WalkthroughSeedData.EnsureAsync(context);
            }
            else if (seedDemoData)
            {
                await SeedData.SeedData.InitializeAsync(context);
                var publicCsvPackPath = app.Configuration["SeedData:PublicCsvPackPath"];
                if (!string.IsNullOrWhiteSpace(publicCsvPackPath))
                {
                    logger.LogInformation("Importing public staging CSV seed pack from {Path}.", publicCsvPackPath);
                    await SeedData.PublicCsvSeedData.EnsureAsync(context, publicCsvPackPath);
                }
                await SeedData.HistoricalTimelineSeedData.EnsureAsync(context);
            }
            else
                logger.LogInformation("Demo data seeding is disabled for this environment.");
            await EnsureResultWorkflowPermissionsAsync(context);
            if (seedDemoData)
            {
                await EnsureSeededTenantSubscriptionsAsync(context);
                await EnsurePremierTenantAdministratorAsync(context);
            }
            await EnsureReportPermissionsAsync(context);
            await EnsureSponsorshipPermissionsAsync(context);
            await EnsureAuditPermissionsAsync(context);
            await EnsurePrivacyPermissionsAsync(context);
            await EnsureSupportPermissionsAsync(context);
            // Reconcile after compatibility upgrades so they cannot
            // reintroduce retired or platform-only tenant grants.
            await EnsureSystemRoleCatalogueAsync(context);
            await BackfillTenantSportsAsync(context);
            await BackfillSportSegmentHierarchiesAsync(context);
            if (seedDemoData)
                await PublishSeededRugbyFixturesAsync(context);
            logger.LogInformation("Database initialization completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }

    private static async Task<bool> RequiresMigrationBaselineAdoptionAsync(
        SportsPlatformDbContext context,
        IReadOnlyCollection<string> migrations,
        ILogger logger)
    {
        if (!context.Database.IsNpgsql()) return false;

        var baseline = migrations.SingleOrDefault(item =>
            item.EndsWith("_PlatformBaseline20260812", StringComparison.Ordinal));
        if (baseline is null)
            throw new InvalidOperationException(
                "The PostgreSQL migration baseline PlatformBaseline20260812 is missing.");

        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT
                    to_regclass('public."Tenants"') IS NOT NULL,
                    to_regclass('public."__EFMigrationsHistory"') IS NOT NULL;
                """;
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            var hasCoreSchema = reader.GetBoolean(0);
            var hasHistoryTable = reader.GetBoolean(1);
            await reader.CloseAsync();
            if (!hasCoreSchema) return false;

            if (hasHistoryTable)
            {
                command.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";";
                var applied = new List<string>();
                await using var historyReader = await command.ExecuteReaderAsync();
                while (await historyReader.ReadAsync())
                    applied.Add(historyReader.GetString(0));
                await historyReader.CloseAsync();
                if (applied.Contains(baseline, StringComparer.Ordinal)) return false;
                if (applied.Count > 0)
                {
                    throw new InvalidOperationException(
                        "The database has an unknown EF Core migration history and cannot be adopted automatically.");
                }
            }
            logger.LogInformation("Existing PostgreSQL schema requires one-time baseline adoption.");
            return true;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task RecordMigrationBaselineAdoptionAsync(
        SportsPlatformDbContext context,
        IReadOnlyCollection<string> migrations,
        ILogger logger)
    {
        var baseline = migrations.Single(item =>
            item.EndsWith("_PlatformBaseline20260812", StringComparison.Ordinal));
        const string productVersion = "9.0.18";
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ({{baseline}}, {{productVersion}})
            ON CONFLICT ("MigrationId") DO NOTHING;
            """);
        logger.LogInformation(
            "Adopted existing PostgreSQL schema at migration baseline {MigrationId}.",
            baseline);
    }

    private static async Task ApplyLegacySchemaCompatibilityAsync(
        SportsPlatformDbContext context)
    {
        await EnsureLiveScoreContextColumnsAsync(context);
        await EnsureGolfScorecardReviewTableAsync(context);
        await EnsureRegistrationFormTablesAsync(context);
        await EnsureCompetitionRegistrationColumnsAsync(context);
        await EnsureGroupQualificationColumnsAsync(context);
        await EnsureBracketProgressionColumnsAsync(context);
        await EnsureSchedulingConstraintTablesAsync(context);
        await EnsureContestLifecyclePolicyTableAsync(context);
        await EnsureContestCheckInTablesAsync(context);
        await EnsureParticipantAbstractionSchemaAsync(context);
        await EnsureBulkImportJobTableAsync(context);
        await EnsureUniversalContestSchemaAsync(context);
        await EnsureTenantUsageCounterTableAsync(context);
        await EnsureSportsCatalogueSchemaAsync(context);
        await EnsurePushDeviceRegistrationTableAsync(context);
        await EnsureNotificationCampaignTableAsync(context);
        await EnsureReportGenerationColumnsAsync(context);
        await EnsureSponsorshipManagementColumnsAsync(context);
        await EnsurePrivacyControlTablesAsync(context);
        await EnsureIdentitySecurityColumnsAsync(context);
        await EnsureIdentityActionTokenTableAsync(context);
    }

    private static async Task EnsureBulkImportJobTableAsync(SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "BulkImportJobs" (
                "Id" uuid NOT NULL, "TenantId" uuid NOT NULL,
                "Kind" character varying(30) NOT NULL, "FileName" character varying(255) NOT NULL,
                "Status" integer NOT NULL, "DuplicateHandling" integer NOT NULL,
                "CreatedBy" uuid NOT NULL, "RosterId" uuid NULL,
                "TotalRows" integer NOT NULL, "ImportedRows" integer NOT NULL,
                "DuplicateRows" integer NOT NULL, "FailedRows" integer NOT NULL,
                "InputJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "ErrorsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "StartedAt" timestamp with time zone NULL, "CompletedAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL, "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_BulkImportJobs" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_BulkImportJobs_TenantId_CreatedAt"
                ON "BulkImportJobs" ("TenantId", "CreatedAt");
            """);
    }

    private static async Task EnsureNotificationCampaignTableAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "NotificationCampaigns" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "Type" integer NOT NULL,
                "Title" character varying(160) NOT NULL,
                "Body" character varying(1000) NOT NULL,
                "IdempotencyKey" character varying(100) NOT NULL,
                "Reason" character varying(500) NOT NULL,
                "CompetitionId" uuid NULL,
                "ContestId" uuid NULL,
                "CreatedBy" uuid NOT NULL,
                "Status" integer NOT NULL,
                "RecipientCount" integer NOT NULL DEFAULT 0,
                "DispatchedAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_NotificationCampaigns" PRIMARY KEY ("Id")
            );
            ALTER TABLE "NotificationCampaigns"
                ADD COLUMN IF NOT EXISTS "Reason" character varying(500) NOT NULL
                    DEFAULT 'Legacy authored campaign';
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationCampaigns_TenantId_IdempotencyKey"
                ON "NotificationCampaigns" ("TenantId", "IdempotencyKey");
            CREATE INDEX IF NOT EXISTS "IX_NotificationCampaigns_TenantId_CreatedAt"
                ON "NotificationCampaigns" ("TenantId", "CreatedAt");
            """);
    }

    private static async Task EnsureSystemRoleCatalogueAsync(
        SportsPlatformDbContext context)
    {
        var roles = await context.Roles
            .Include(role => role.Permissions)
            .ToListAsync();
        foreach (var retired in roles.SelectMany(role => role.Permissions)
                     .Where(permission => !SystemPermissions.IsKnown(permission.Permission))
                     .ToArray())
            context.RolePermissions.Remove(retired);

        Role EnsureRole(
            SystemRoleDefinition definition,
            Guid? tenantId)
        {
            var role = roles.FirstOrDefault(candidate =>
                candidate.TenantId == tenantId
                && candidate.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase));
            if (role is null)
            {
                role = new Role(
                    Guid.NewGuid(),
                    definition.Name,
                    definition.Description,
                    isSystemRole: true,
                    tenantId: tenantId);
                context.Roles.Add(role);
                roles.Add(role);
            }
            else if (!role.IsSystemRole)
            {
                role.MarkAsSystemRole();
            }

            var required = definition.Permissions.ToHashSet(StringComparer.Ordinal);
            foreach (var existing in role.Permissions
                         .Where(permission => !required.Contains(permission.Permission))
                         .ToArray())
                context.RolePermissions.Remove(existing);
            foreach (var permission in required)
            {
                if (!role.Permissions.Any(existing => existing.Permission == permission))
                    context.RolePermissions.Add(
                        new RolePermission(Guid.NewGuid(), role.Id, permission));
            }
            return role;
        }

        var platformRoles = new Dictionary<string, Role>(StringComparer.Ordinal);
        foreach (var definition in SystemRoleCatalogue.PlatformRoles)
            platformRoles[definition.Name] = EnsureRole(definition, null);

        var tenantRoleMap = new Dictionary<(Guid TenantId, string Name), Role>();
        var tenantIds = await context.Tenants.Select(tenant => tenant.Id).ToArrayAsync();
        foreach (var tenantId in tenantIds)
        foreach (var definition in SystemRoleCatalogue.TenantRoles)
            tenantRoleMap[(tenantId, definition.Name)] = EnsureRole(definition, tenantId);

        // Upgrade demo/legacy assignments to the authoritative scoped roles
        // without invalidating compatibility assignments already in use.
        var legacyAssignments = await context.UserRoles
            .Join(context.Roles,
                assignment => assignment.RoleId,
                role => role.Id,
                (assignment, role) => new { assignment.UserId, role.Name, role.TenantId })
            .Join(context.Users,
                assignment => assignment.UserId,
                user => user.Id,
                (assignment, user) => new { assignment.UserId, assignment.Name, assignment.TenantId, UserTenantId = user.TenantId })
            .Where(item => item.Name == "Admin" || item.Name == SystemRoles.Scorer)
            .ToArrayAsync();
        foreach (var assignment in legacyAssignments)
        {
            Role? target = null;
            if (assignment.Name == "Admin")
                target = platformRoles[SystemRoles.PlatformOwner];
            else if (assignment.UserTenantId.HasValue)
                tenantRoleMap.TryGetValue(
                    (assignment.UserTenantId.Value, SystemRoles.Scorer), out target);

            if (target is not null && !await context.UserRoles.AnyAsync(existing =>
                    existing.UserId == assignment.UserId && existing.RoleId == target.Id))
                context.UserRoles.Add(new UserRole(Guid.NewGuid(), assignment.UserId, target.Id));
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureResultWorkflowPermissionsAsync(
        SportsPlatformDbContext context)
    {
        var roles = await context.Roles
            .Where(item => item.Name == "Admin" || item.Name == SystemRoles.Scorer)
            .ToArrayAsync();
        foreach (var role in roles)
        {
            var required = role.Name == "Admin"
                ? new[] { "results.submit", "results.approve" }
                : new[] { "results.submit" };
            foreach (var permission in required)
            {
                if (!await context.RolePermissions.AnyAsync(item =>
                        item.RoleId == role.Id && item.Permission == permission))
                {
                    context.RolePermissions.Add(
                        new RolePermission(Guid.NewGuid(), role.Id, permission));
                }
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsureReportPermissionsAsync(
        SportsPlatformDbContext context)
    {
        var roles = await context.Roles
            .Include(item => item.Permissions)
            .Where(item => item.Name == "Admin"
                || item.Name == SystemRoles.TenantOwner
                || item.Name == SystemRoles.CompetitionAdministrator)
            .ToArrayAsync();
        foreach (var role in roles)
        foreach (var permission in new[] { "reports.read", "reports.export" })
        {
            if (!role.Permissions.Any(item => item.Permission == permission))
                context.RolePermissions.Add(
                    new RolePermission(Guid.NewGuid(), role.Id, permission));
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsureSponsorshipPermissionsAsync(
        SportsPlatformDbContext context)
    {
        var roles = await context.Roles
            .Include(item => item.Permissions)
            .Where(item => item.Name == "Admin"
                || item.Name == SystemRoles.TenantOwner
                || item.Name == SystemRoles.CompetitionAdministrator)
            .ToArrayAsync();
        foreach (var role in roles)
        foreach (var permission in new[] { "sponsorships.read", "sponsorships.manage" })
        {
            if (!role.Permissions.Any(item => item.Permission == permission))
                context.RolePermissions.Add(
                    new RolePermission(Guid.NewGuid(), role.Id, permission));
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsureAuditPermissionsAsync(
        SportsPlatformDbContext context)
    {
        var roles = await context.Roles
            .Include(item => item.Permissions)
            .Where(item => item.Name == "Admin"
                || item.Name == SystemRoles.TenantOwner
                || item.Name == SystemRoles.PlatformSupport)
            .ToArrayAsync();
        foreach (var role in roles)
        {
            if (!role.Permissions.Any(item => item.Permission == "audit.read"))
                context.RolePermissions.Add(
                    new RolePermission(Guid.NewGuid(), role.Id, "audit.read"));
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsurePrivacyPermissionsAsync(
        SportsPlatformDbContext context)
    {
        var roles = await context.Roles
            .Include(item => item.Permissions)
            .Where(item => item.Name == "Admin"
                || item.Name == SystemRoles.TenantOwner
                || item.Name == SystemRoles.PlatformSupport)
            .ToArrayAsync();
        foreach (var role in roles)
        foreach (var permission in new[] { "privacy.read", "privacy.manage" })
        {
            if (!role.Permissions.Any(item => item.Permission == permission))
                context.RolePermissions.Add(
                    new RolePermission(Guid.NewGuid(), role.Id, permission));
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsureSupportPermissionsAsync(
        SportsPlatformDbContext context)
    {
        var roles = await context.Roles
            .Include(item => item.Permissions)
            .Where(item => item.Name == "Admin"
                || item.Name == SystemRoles.PlatformOwner
                || item.Name == SystemRoles.PlatformSupport)
            .ToArrayAsync();
        foreach (var role in roles)
        foreach (var permission in new[] { "support.read", "support.manage" })
        {
            if (!role.Permissions.Any(item => item.Permission == permission))
                context.RolePermissions.Add(
                    new RolePermission(Guid.NewGuid(), role.Id, permission));
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsurePrivacyControlTablesAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "ConsentRecords" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "TenantId" uuid NULL,
                "Kind" integer NOT NULL,
                "Granted" boolean NOT NULL,
                "PolicyVersion" character varying(80) NOT NULL,
                "Source" character varying(80) NOT NULL,
                "IpAddress" character varying(50) NULL,
                "OccurredAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ConsentRecords" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ConsentRecords_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_ConsentRecords_TenantId_OccurredAt" ON "ConsentRecords" ("TenantId", "OccurredAt");
            CREATE INDEX IF NOT EXISTS "IX_ConsentRecords_UserId_Kind_OccurredAt" ON "ConsentRecords" ("UserId", "Kind", "OccurredAt");

            CREATE TABLE IF NOT EXISTS "PrivacyControlSettings" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "TenantId" uuid NULL,
                "ProfileVisibility" integer NOT NULL,
                "ShowActivity" boolean NOT NULL,
                "AllowAnalytics" boolean NOT NULL,
                "AllowMarketing" boolean NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PrivacyControlSettings" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PrivacyControlSettings_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PrivacyControlSettings_UserId" ON "PrivacyControlSettings" ("UserId");

            CREATE TABLE IF NOT EXISTS "PrivacyActionRequests" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "TenantId" uuid NULL,
                "Kind" integer NOT NULL,
                "Status" integer NOT NULL,
                "Details" character varying(2000) NOT NULL,
                "Response" character varying(2000) NULL,
                "ExportPayload" text NULL,
                "RequestedAt" timestamp with time zone NOT NULL,
                "ReviewedAt" timestamp with time zone NULL,
                "ReviewedBy" uuid NULL,
                "CompletedAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PrivacyActionRequests" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PrivacyActionRequests_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_PrivacyActionRequests_TenantId_Status_RequestedAt" ON "PrivacyActionRequests" ("TenantId", "Status", "RequestedAt");
            CREATE INDEX IF NOT EXISTS "IX_PrivacyActionRequests_UserId_RequestedAt" ON "PrivacyActionRequests" ("UserId", "RequestedAt");

            CREATE TABLE IF NOT EXISTS "TenantRetentionPolicies" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "Name" character varying(160) NOT NULL,
                "Resource" integer NOT NULL,
                "RetentionDays" integer NOT NULL,
                "Action" integer NOT NULL,
                "IsActive" boolean NOT NULL,
                "RunIntervalHours" integer NOT NULL,
                "NextRunAt" timestamp with time zone NOT NULL,
                "LastRunAt" timestamp with time zone NULL,
                "LastAffectedCount" integer NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_TenantRetentionPolicies" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TenantRetentionPolicies_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantRetentionPolicies_TenantId_Resource" ON "TenantRetentionPolicies" ("TenantId", "Resource");
            CREATE INDEX IF NOT EXISTS "IX_TenantRetentionPolicies_IsActive_NextRunAt" ON "TenantRetentionPolicies" ("IsActive", "NextRunAt");
            """);
    }

    private static async Task EnsureTenantUsageCounterTableAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "TenantUsageCounters" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "Metric" character varying(100) NOT NULL,
                "PeriodStartUtc" timestamp with time zone NOT NULL,
                "Value" bigint NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_TenantUsageCounters" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantUsageCounters_TenantId_Metric_PeriodStartUtc"
                ON "TenantUsageCounters" ("TenantId", "Metric", "PeriodStartUtc");
            CREATE INDEX IF NOT EXISTS "IX_TenantUsageCounters_TenantId"
                ON "TenantUsageCounters" ("TenantId");
            """);
    }

    private static async Task EnsureSportsCatalogueSchemaAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Sports"
                ADD COLUMN IF NOT EXISTS "SupportedParticipantShapes" jsonb NOT NULL DEFAULT '[]'::jsonb;
            UPDATE "Sports" SET "SupportedParticipantShapes" = CASE "Code"
                WHEN 'GOLF' THEN '["individual"]'::jsonb
                WHEN 'ATHLETICS' THEN '["individual","team"]'::jsonb
                WHEN 'MMA' THEN '["individual"]'::jsonb
                ELSE '["team"]'::jsonb
            END
            WHERE "SupportedParticipantShapes" = '[]'::jsonb;

            CREATE TABLE IF NOT EXISTS "TenantSports" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "SportId" uuid NOT NULL,
                "IsEnabled" boolean NOT NULL DEFAULT true,
                "EnabledAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_TenantSports" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TenantSports_Sports_SportId" FOREIGN KEY ("SportId")
                    REFERENCES "Sports" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_TenantSports_Tenants_TenantId" FOREIGN KEY ("TenantId")
                    REFERENCES "Tenants" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantSports_TenantId_SportId"
                ON "TenantSports" ("TenantId", "SportId");
            CREATE INDEX IF NOT EXISTS "IX_TenantSports_SportId"
                ON "TenantSports" ("SportId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SportDefinitionVersions_SportId_VersionNumber"
                ON "SportDefinitionVersions" ("SportId", "VersionNumber");
            """);
    }

    private static async Task BackfillTenantSportsAsync(SportsPlatformDbContext context)
    {
        if (context.Database.IsNpgsql())
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "TenantSports"
                    ("Id", "TenantId", "SportId", "IsEnabled", "EnabledAt", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), source."TenantId", source."SportId", true, NOW(), NOW(), NOW()
                FROM (SELECT DISTINCT "TenantId", "SportId" FROM "Competitions") AS source
                ON CONFLICT ("TenantId", "SportId") DO NOTHING;
                """);
            return;
        }

        var pairs = await context.Competitions
            .Select(item => new { item.TenantId, item.SportId })
            .Distinct()
            .ToArrayAsync();
        var existing = await context.TenantSports
            .Select(item => new { item.TenantId, item.SportId })
            .ToArrayAsync();
        foreach (var pair in pairs.Where(pair => !existing.Any(item =>
                     item.TenantId == pair.TenantId && item.SportId == pair.SportId)))
            context.TenantSports.Add(new SportsPlatform.Modules.Sports.Entities.TenantSport(
                Guid.NewGuid(), pair.TenantId, pair.SportId));
        await context.SaveChangesAsync();
    }

    private static async Task EnsurePushDeviceRegistrationTableAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PushDeviceRegistrations" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Token" character varying(2048) NOT NULL,
                "Platform" character varying(30) NOT NULL,
                "DeviceId" character varying(200) NULL,
                "IsEnabled" boolean NOT NULL,
                "LastSeenAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PushDeviceRegistrations" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PushDeviceRegistrations_Token"
                ON "PushDeviceRegistrations" ("Token");
            CREATE INDEX IF NOT EXISTS "IX_PushDeviceRegistrations_UserId_IsEnabled"
                ON "PushDeviceRegistrations" ("UserId", "IsEnabled");
            """);
    }

    private static async Task EnsureReportGenerationColumnsAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Reports"
                ADD COLUMN IF NOT EXISTS "TenantId" uuid NOT NULL
                    DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;
            ALTER TABLE "Reports"
                ADD COLUMN IF NOT EXISTS "GeneratedContent" bytea NULL;
            ALTER TABLE "Reports"
                ADD COLUMN IF NOT EXISTS "GeneratedContentType" character varying(150) NULL;
              ALTER TABLE "Reports"
                  ADD COLUMN IF NOT EXISTS "GeneratedFileName" character varying(260) NULL;
              UPDATE "Reports" AS report
              SET "TenantId" = app_user."TenantId"
              FROM "Users" AS app_user
              WHERE report."CreatedBy" = app_user."Id"
                AND report."TenantId" = '00000000-0000-0000-0000-000000000000'::uuid
                AND app_user."TenantId" IS NOT NULL;
              CREATE INDEX IF NOT EXISTS "IX_Reports_TenantId_CreatedAt"
                ON "Reports" ("TenantId", "CreatedAt" DESC);
            """);
    }

    private static async Task EnsureSponsorshipManagementColumnsAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Sponsors"
                ADD COLUMN IF NOT EXISTS "TenantId" uuid NOT NULL
                    DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;
            ALTER TABLE "Sponsorships"
                ADD COLUMN IF NOT EXISTS "TenantId" uuid NOT NULL
                    DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;
            ALTER TABLE "Sponsorships"
                ADD COLUMN IF NOT EXISTS "Placement" character varying(50) NOT NULL
                    DEFAULT 'PublicHome';
            ALTER TABLE "Sponsorships"
                ADD COLUMN IF NOT EXISTS "DisplayOrder" integer NOT NULL DEFAULT 0;

            UPDATE "Sponsorships" AS sponsorship
            SET "TenantId" = competition."TenantId"
            FROM "Competitions" AS competition
            WHERE sponsorship."CompetitionId" = competition."Id"
              AND sponsorship."TenantId" = '00000000-0000-0000-0000-000000000000'::uuid;
            UPDATE "Sponsorships" AS sponsorship
            SET "TenantId" = contest."TenantId"
            FROM "Contests" AS contest
            WHERE sponsorship."ContestId" = contest."Id"
              AND sponsorship."TenantId" = '00000000-0000-0000-0000-000000000000'::uuid;
            UPDATE "Sponsors" AS sponsor
            SET "TenantId" = sponsorship."TenantId"
            FROM "Sponsorships" AS sponsorship
            WHERE sponsorship."SponsorId" = sponsor."Id"
              AND sponsor."TenantId" = '00000000-0000-0000-0000-000000000000'::uuid
              AND sponsorship."TenantId" <> '00000000-0000-0000-0000-000000000000'::uuid;

            CREATE INDEX IF NOT EXISTS "IX_Sponsors_TenantId_Name"
                ON "Sponsors" ("TenantId", "Name");
            CREATE INDEX IF NOT EXISTS "IX_Sponsors_TenantId_IsActive"
                ON "Sponsors" ("TenantId", "IsActive");
            CREATE INDEX IF NOT EXISTS "IX_Sponsorships_TenantId_Status_StartDate_EndDate"
                ON "Sponsorships" ("TenantId", "Status", "StartDate", "EndDate");
            CREATE INDEX IF NOT EXISTS "IX_Sponsorships_TenantId_Placement_DisplayOrder"
                ON "Sponsorships" ("TenantId", "Placement", "DisplayOrder");
            """);
    }

    private static async Task EnsureAuditAppendOnlyAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE OR REPLACE FUNCTION prevent_audit_entry_mutation()
            RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'AuditEntries are append-only';
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS "TR_AuditEntries_AppendOnly" ON "AuditEntries";
            CREATE TRIGGER "TR_AuditEntries_AppendOnly"
                BEFORE UPDATE OR DELETE ON "AuditEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_entry_mutation();
            """);
    }

    private static async Task EnsureIdentitySecurityColumnsAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Users"
                ADD COLUMN IF NOT EXISTS "MfaEnabled" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Users"
                ADD COLUMN IF NOT EXISTS "MfaSecretProtected" text NULL;
            ALTER TABLE "Users"
                ADD COLUMN IF NOT EXISTS "MfaEnrolledAt" timestamp with time zone NULL;
            """);
    }

    private static async Task EnsureIdentityActionTokenTableAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "IdentityActionTokens" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Purpose" character varying(40) NOT NULL,
                "TokenHash" character varying(64) NOT NULL,
                "ExpiresAt" timestamp with time zone NOT NULL,
                "ConsumedAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_IdentityActionTokens" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_IdentityActionTokens_Users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_IdentityActionTokens_TokenHash"
                ON "IdentityActionTokens" ("TokenHash");
            CREATE INDEX IF NOT EXISTS "IX_IdentityActionTokens_UserId_Purpose_ConsumedAt"
                ON "IdentityActionTokens" ("UserId", "Purpose", "ConsumedAt");
            """);
    }

    private static async Task EnsureSeededTenantSubscriptionsAsync(
        SportsPlatformDbContext context)
    {
        var seededTenantIds = Enumerable.Range(1, 8)
            .Select(index => Guid.Parse($"00000000-0000-0000-0000-{index:D12}"))
            .ToHashSet();
        var tenantIds = await context.Tenants
            .Where(tenant => seededTenantIds.Contains(tenant.Id))
            .Select(tenant => tenant.Id)
            .ToListAsync();
        var subscribedTenantIds = await context.Subscriptions
            .Where(subscription => subscription.Status != SubscriptionStatus.Draft &&
                                   subscription.Status != SubscriptionStatus.Archived)
            .Select(subscription => subscription.TenantId)
            .Distinct()
            .ToListAsync();

        foreach (var tenantId in tenantIds.Except(subscribedTenantIds))
        {
            var subscription = new Subscription(
                Guid.NewGuid(),
                tenantId,
                "Professional",
                new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            subscription.Activate();
            context.Subscriptions.Add(subscription);
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureLiveScoreContextColumnsAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "SegmentId" uuid NULL;
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "ClockSeconds" numeric NULL;
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "EventType" character varying(100) NOT NULL DEFAULT 'score';
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "SecondaryParticipantId" uuid NULL;
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "DeviceId" character varying(200) NOT NULL DEFAULT 'legacy';
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "IdempotencyKey" character varying(200) NOT NULL DEFAULT '';
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "RulesetVersionId" uuid NULL;
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "DeviceTimestamp" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
            ALTER TABLE "LiveScores"
                ADD COLUMN IF NOT EXISTS "ServerTimestamp" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
            UPDATE "LiveScores" score
                SET "TenantId" = contest."TenantId"
                FROM "Contests" contest
                WHERE score."ContestId" = contest."Id"
                  AND score."TenantId" = '00000000-0000-0000-0000-000000000000';
            UPDATE "LiveScores"
                SET "EventType" = "MetricName"
                WHERE "EventType" = 'score' AND "MetricName" <> 'score';
            UPDATE "LiveScores"
                SET "IdempotencyKey" = "EventId"::text
                WHERE "IdempotencyKey" = '';
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveScores_ContestId_EventId"
                ON "LiveScores" ("ContestId", "EventId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveScores_ContestId_IdempotencyKey"
                ON "LiveScores" ("ContestId", "IdempotencyKey");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveScores_ContestId_DeviceId_Sequence"
                ON "LiveScores" ("ContestId", "DeviceId", "Sequence");
            ALTER TABLE "LiveScoreSessions"
                ADD COLUMN IF NOT EXISTS "ActiveSegmentId" uuid NULL;
            ALTER TABLE "LiveScoreSessions"
                ADD COLUMN IF NOT EXISTS "ClockSeconds" numeric NOT NULL DEFAULT 0;
            ALTER TABLE "LiveScoreSessions"
                ADD COLUMN IF NOT EXISTS "ClockCountsDown" boolean NOT NULL DEFAULT false;
            ALTER TABLE "LiveScoreSessions"
                ADD COLUMN IF NOT EXISTS "IsClockRunning" boolean NOT NULL DEFAULT false;
            ALTER TABLE "LiveScoreSessions"
                ADD COLUMN IF NOT EXISTS "ClockStartedAt" timestamp with time zone NULL;
            ALTER TABLE "ContestSegments"
                ADD COLUMN IF NOT EXISTS "IsOptional" boolean NOT NULL DEFAULT false;
            ALTER TABLE "ContestSegments"
                ADD COLUMN IF NOT EXISTS "ActivationCondition" character varying(200) NULL;
            ALTER TABLE "ContestNestedSegments"
                ADD COLUMN IF NOT EXISTS "IsOptional" boolean NOT NULL DEFAULT false;
            ALTER TABLE "ContestNestedSegments"
                ADD COLUMN IF NOT EXISTS "ActivationCondition" character varying(200) NULL;
            """);
    }

    private static async Task EnsureGolfScorecardReviewTableAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "GolfScorecardReviews"
            (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "ContestId" uuid NOT NULL,
                "ParticipantId" uuid NOT NULL,
                "RoundNumber" integer NOT NULL,
                "HolesJson" jsonb NOT NULL,
                "Status" character varying(30) NOT NULL,
                "SubmittedBy" uuid NOT NULL,
                "SubmittedAt" timestamp with time zone NOT NULL,
                "ReviewedBy" uuid NULL,
                "ReviewedAt" timestamp with time zone NULL,
                "ReviewNotes" character varying(1000) NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_GolfScorecardReviews" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_GolfScorecardReviews_ContestParticipantRoundStatus"
                ON "GolfScorecardReviews" ("ContestId", "ParticipantId", "RoundNumber", "Status");
            """);
    }

    private static async Task EnsurePremierTenantAdministratorAsync(
        SportsPlatformDbContext context)
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var roleId = Guid.Parse("00000000-0000-0000-0000-000000000014");
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000027");
        var userRoleId = Guid.Parse("00000000-0000-0000-0000-000000000037");
        var role = await context.Roles
            .Include(item => item.Permissions)
            .FirstOrDefaultAsync(item => item.Id == roleId);
        if (role is null)
        {
            role = new Role(
                roleId,
                SystemRoles.TenantOwner,
                "Tenant-scoped administrator for the Premier League demo",
                isSystemRole: false,
                tenantId: tenantId);
            context.Roles.Add(role);
        }

        var permissions = new[]
        {
            "sports.read", "sports.create", "sports.manage",
            "competitions.read", "competitions.create", "competitions.manage",
            "contests.read", "contests.create", "contests.manage",
            "participants.read", "participants.create", "participants.manage",
            "tenants.read", "tenants.update",
            "subscriptions.read", "subscriptions.manage",
            "users.read", "users.manage", "roles.read", "roles.manage",
            "roles.create", "roles.update", "roles.delete", "roles.assign",
            "branding.read", "branding.manage",
            "sponsorships.read", "sponsorships.manage",
            "audit.read",
            "privacy.read", "privacy.manage",
            "scoring.manage", "scoring.correct", "results.manage",
            "results.submit", "results.approve",
            "reports.read", "reports.export"
        };
        foreach (var permission in permissions)
        {
            if (!role.Permissions.Any(item => item.Permission == permission))
            {
                context.RolePermissions.Add(
                    new RolePermission(Guid.NewGuid(), role.Id, permission));
            }
        }

        var user = await context.Users.FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            user = new User(
                userId,
                "admin@premier.com",
                "Premier",
                "Administrator",
                "TenantAdmin123!",
                tenantId);
            context.Users.Add(user);
        }
        if (!await context.UserRoles.AnyAsync(item =>
                item.UserId == userId && item.RoleId == roleId))
        {
            context.UserRoles.Add(new UserRole(userRoleId, userId, roleId));
        }

        await context.SaveChangesAsync();
    }

    private static async Task PublishSeededRugbyFixturesAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Contests"
            SET "Status" = 'Scheduled',
                "Venue" = COALESCE("Venue", 'Six Nations venue TBC'),
                "StartTime" = COALESCE(
                    "StartTime",
                    TIMESTAMPTZ '2026-02-05 20:00:00+00'
                        + (("SortOrder" - 6001) * INTERVAL '3 days')),
                "UpdatedAt" = NOW()
            WHERE "SportId" = '00000000-0000-0000-0000-000000000106'::uuid
              AND "TenantId" = '00000000-0000-0000-0000-000000000008'::uuid
              AND "Status" = 'Draft';
            """);
    }

    private static async Task BackfillSportSegmentHierarchiesAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE "ContestSegments" AS segment
            SET "Name" = 'Innings ' || segment."SortOrder",
                "SegmentType" = 'Innings'
            FROM "Contests" AS contest
            WHERE contest."Id" = segment."ContestId"
              AND contest."SportId" = '00000000-0000-0000-0000-000000000104'::uuid;

            UPDATE "ContestSegments" AS segment
            SET "Name" = CASE segment."SortOrder"
                    WHEN 1 THEN 'First Half'
                    WHEN 2 THEN 'Second Half'
                    ELSE segment."Name"
                END,
                "SegmentType" = CASE
                    WHEN segment."SortOrder" IN (1, 2) THEN 'Half'
                    ELSE segment."SegmentType"
                END
            FROM "Contests" AS contest
            WHERE contest."Id" = segment."ContestId"
              AND contest."SportId" = '00000000-0000-0000-0000-000000000100'::uuid;

            INSERT INTO "ContestSegments"
                ("Id", "ContestId", "Name", "SegmentType", "SortOrder", "Status",
                 "StartedAt", "EndedAt", "CreatedAt", "UpdatedAt", "TenantId",
                 "IsOptional", "ActivationCondition")
            SELECT gen_random_uuid(), contest."Id", 'Extra Time', 'Overtime', 3,
                   'Pending', NULL, NULL, NOW(), NULL, contest."TenantId",
                   true, 'tie_after_regulation'
            FROM "Contests" AS contest
            WHERE contest."SportId" = '00000000-0000-0000-0000-000000000100'::uuid
              AND NOT EXISTS (
                  SELECT 1 FROM "ContestSegments" AS segment
                  WHERE segment."ContestId" = contest."Id"
                    AND segment."SegmentType" = 'Overtime');

            UPDATE "ContestSegments" AS segment
            SET "Name" = 'Quarter ' || segment."SortOrder",
                "SegmentType" = 'Quarter'
            FROM "Contests" AS contest
            WHERE contest."Id" = segment."ContestId"
              AND contest."SportId" = '00000000-0000-0000-0000-000000000101'::uuid
              AND segment."SortOrder" <= 4;

            INSERT INTO "ContestSegments"
                ("Id", "ContestId", "Name", "SegmentType", "SortOrder", "Status",
                 "StartedAt", "EndedAt", "CreatedAt", "UpdatedAt", "TenantId",
                 "IsOptional", "ActivationCondition")
            SELECT gen_random_uuid(), contest."Id", 'Quarter ' || number, 'Quarter',
                   number, 'Pending', NULL, NULL, NOW(), NULL, contest."TenantId",
                   false, NULL
            FROM "Contests" AS contest
            CROSS JOIN generate_series(1, 4) AS series(number)
            WHERE contest."SportId" = '00000000-0000-0000-0000-000000000101'::uuid
              AND NOT EXISTS (
                  SELECT 1 FROM "ContestSegments" AS segment
                  WHERE segment."ContestId" = contest."Id"
                    AND segment."SortOrder" = number);

            INSERT INTO "ContestSegments"
                ("Id", "ContestId", "Name", "SegmentType", "SortOrder", "Status",
                 "StartedAt", "EndedAt", "CreatedAt", "UpdatedAt", "TenantId",
                 "IsOptional", "ActivationCondition")
            SELECT gen_random_uuid(), contest."Id", 'Overtime', 'Overtime', 5,
                   'Pending', NULL, NULL, NOW(), NULL, contest."TenantId",
                   true, 'tie_after_regulation'
            FROM "Contests" AS contest
            WHERE contest."SportId" = '00000000-0000-0000-0000-000000000101'::uuid
              AND NOT EXISTS (
                  SELECT 1 FROM "ContestSegments" AS segment
                  WHERE segment."ContestId" = contest."Id"
                    AND segment."SegmentType" = 'Overtime');

            INSERT INTO "ContestNestedSegments"
                ("Id", "ContestId", "Name", "SegmentType", "SortOrder", "Status",
                 "StartedAt", "EndedAt", "ContestSegmentId", "CreatedAt", "UpdatedAt",
                 "TenantId", "IsOptional", "ActivationCondition")
            SELECT gen_random_uuid(), parent."ContestId", 'Hole ' || number, 'Hole',
                   number, 'Pending', NULL, NULL, parent."Id", NOW(), NULL,
                   parent."TenantId", false, NULL
            FROM "ContestSegments" AS parent
            INNER JOIN "Contests" AS contest ON contest."Id" = parent."ContestId"
            CROSS JOIN generate_series(1, 18) AS series(number)
            WHERE contest."SportId" = '00000000-0000-0000-0000-000000000102'::uuid
              AND NOT EXISTS (
                  SELECT 1 FROM "ContestNestedSegments" AS nested
                  WHERE nested."ContestSegmentId" = parent."Id"
                    AND nested."SortOrder" = number);

            INSERT INTO "ContestNestedSegments"
                ("Id", "ContestId", "Name", "SegmentType", "SortOrder", "Status",
                 "StartedAt", "EndedAt", "ContestSegmentId", "CreatedAt", "UpdatedAt",
                 "TenantId", "IsOptional", "ActivationCondition")
            SELECT gen_random_uuid(), parent."ContestId", 'Over ' || number, 'Over',
                   number, 'Pending', NULL, NULL, parent."Id", NOW(), NULL,
                   parent."TenantId", false, NULL
            FROM "ContestSegments" AS parent
            INNER JOIN "Contests" AS contest ON contest."Id" = parent."ContestId"
            CROSS JOIN generate_series(1, 20) AS series(number)
            WHERE contest."SportId" = '00000000-0000-0000-0000-000000000104'::uuid
              AND NOT EXISTS (
                  SELECT 1 FROM "ContestNestedSegments" AS nested
                  WHERE nested."ContestSegmentId" = parent."Id"
                    AND nested."SortOrder" = number);
            """);
    }

    private static async Task EnsureRegistrationFormTablesAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "RegistrationForms"
            (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "CompetitionId" uuid NULL,
                "Name" text NOT NULL,
                "Description" text NULL,
                "FormSchema" text NOT NULL,
                "IsActive" boolean NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                "CreatedBy" text NOT NULL,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_RegistrationForms" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_RegistrationForms_TenantId"
                ON "RegistrationForms" ("TenantId");
            CREATE INDEX IF NOT EXISTS "IX_RegistrationForms_CompetitionId"
                ON "RegistrationForms" ("CompetitionId");

            CREATE TABLE IF NOT EXISTS "RegistrationFormSubmissions"
            (
                "Id" uuid NOT NULL,
                "FormId" uuid NOT NULL,
                "ParticipantId" uuid NOT NULL,
                "ContestId" uuid NULL,
                "FormData" text NOT NULL,
                "Status" integer NOT NULL,
                "ReviewNotes" text NULL,
                "ReviewedBy" uuid NULL,
                "ReviewedAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                "SubmittedAt" timestamp with time zone NULL,
                "FormVersion" integer NOT NULL DEFAULT 1,
                "FormSchemaSnapshot" text NOT NULL DEFAULT '[]',
                "Revision" integer NOT NULL DEFAULT 1,
                CONSTRAINT "PK_RegistrationFormSubmissions" PRIMARY KEY ("Id")
            );

            ALTER TABLE "RegistrationFormSubmissions"
                ADD COLUMN IF NOT EXISTS "FormVersion" integer NOT NULL DEFAULT 1;
            ALTER TABLE "RegistrationFormSubmissions"
                ADD COLUMN IF NOT EXISTS "FormSchemaSnapshot" text NOT NULL DEFAULT '[]';
            ALTER TABLE "RegistrationFormSubmissions"
                ADD COLUMN IF NOT EXISTS "Revision" integer NOT NULL DEFAULT 1;
            UPDATE "RegistrationFormSubmissions" AS submission
                SET "FormVersion" = form."Version",
                    "FormSchemaSnapshot" = form."FormSchema"
                FROM "RegistrationForms" AS form
                WHERE submission."FormId" = form."Id"
                  AND submission."FormSchemaSnapshot" = '[]';

            CREATE INDEX IF NOT EXISTS "IX_RegistrationFormSubmissions_FormId"
                ON "RegistrationFormSubmissions" ("FormId");
            CREATE INDEX IF NOT EXISTS "IX_RegistrationFormSubmissions_ParticipantId"
                ON "RegistrationFormSubmissions" ("ParticipantId");
            """);
    }

    private static async Task EnsureCompetitionRegistrationColumnsAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Registrations"
                ALTER COLUMN "ContestId" DROP NOT NULL;
            ALTER TABLE "Registrations"
                ADD COLUMN IF NOT EXISTS "CompetitionId" uuid NULL;
            ALTER TABLE "Registrations"
                ADD COLUMN IF NOT EXISTS "SubmittedBy" uuid NULL;
            ALTER TABLE "Registrations"
                ADD COLUMN IF NOT EXISTS "EligibilityIssuesJson" jsonb NOT NULL DEFAULT '[]'::jsonb;
            ALTER TABLE "Registrations"
                ADD COLUMN IF NOT EXISTS "EligibilityCheckedAt" timestamp with time zone NULL;
            ALTER TABLE "Registrations"
                ADD COLUMN IF NOT EXISTS "EligibilityOverriddenBy" uuid NULL;
            ALTER TABLE "Registrations"
                ADD COLUMN IF NOT EXISTS "EligibilityOverriddenAt" timestamp with time zone NULL;
            ALTER TABLE "Registrations"
                ADD COLUMN IF NOT EXISTS "EligibilityOverrideReason" character varying(1000) NULL;

            CREATE TABLE IF NOT EXISTS "CompetitionEligibilityPolicies" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "CompetitionId" uuid NOT NULL,
                "MinimumSquadSize" integer NULL,
                "MaximumSquadSize" integer NULL,
                "MinimumAge" integer NULL,
                "MaximumAge" integer NULL,
                "MinimumWeightKg" numeric(8,2) NULL,
                "MaximumWeightKg" numeric(8,2) NULL,
                "RegistrationDeadline" timestamp with time zone NULL,
                "RequiredDocumentTypesJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "RequireSquadNumbers" boolean NOT NULL DEFAULT false,
                "RequireUniqueSquadNumbers" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_CompetitionEligibilityPolicies" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CompetitionEligibilityPolicies_CompetitionId"
                ON "CompetitionEligibilityPolicies" ("CompetitionId");

            CREATE INDEX IF NOT EXISTS "IX_Registrations_CompetitionId"
                ON "Registrations" ("CompetitionId");
            CREATE UNIQUE INDEX IF NOT EXISTS
                "IX_Registrations_CompetitionId_ParticipantId"
                ON "Registrations" ("CompetitionId", "ParticipantId")
                WHERE "CompetitionId" IS NOT NULL;
            """);
    }

    private static async Task EnsureBracketProgressionColumnsAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Brackets"
                ADD COLUMN IF NOT EXISTS "ChampionId" uuid NULL;
            ALTER TABLE "Brackets"
                ADD COLUMN IF NOT EXISTS "DrawSeed" integer NULL;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "ContestId" uuid NULL;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "NextMatchId" uuid NULL;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "NextMatchSlot" integer NULL;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "SeriesId" uuid NULL;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "BestOf" integer NOT NULL DEFAULT 1;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "IsAggregateTie" boolean NOT NULL DEFAULT false;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "IsThirdPlace" boolean NOT NULL DEFAULT false;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "LoserNextMatchId" uuid NULL;
            ALTER TABLE "BracketMatches"
                ADD COLUMN IF NOT EXISTS "LoserNextMatchSlot" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "SeriesId" uuid NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "SeriesGameNumber" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "SeriesBestOf" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "SeriesHomeParticipantSlot" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "ParPerRound" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "CutAfterRound" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "CutToParticipants" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "CutAppliedAt" timestamp with time zone NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "CourseName" character varying(200) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "CourseLocation" character varying(300) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "HoleParsJson" jsonb NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "WeightClass" character varying(100) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "CardSection" character varying(50) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "ScheduledRounds" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "ResultMethod" character varying(100) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "ResultRound" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "ResultTimeSeconds" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "ResultNotes" character varying(500) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AllowedResultMethods" character varying(1000) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AthleticsEventName" character varying(150) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "DistanceMeters" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "QualifyByPlace" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "QualifyByTime" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AdvanceQualificationTies" boolean NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AdvancePlaceTies" boolean NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AdvanceTimeTies" boolean NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AthleticsLanesPerHeat" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AthleticsLaneAssignment" character varying(20) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "NextRoundHeatCount" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "NextRoundQualifyByPlace" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "NextRoundQualifyByTime" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "QualificationAppliedAt" timestamp with time zone NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AthleticsMetricType" character varying(20) NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AthleticsMaxAttempts" integer NULL;
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AthleticsRecordThreshold" numeric(12,3) NULL;
            ALTER TABLE "ContestParticipants"
                ADD COLUMN IF NOT EXISTS "MadeCut" boolean NULL;
            ALTER TABLE "ContestParticipants"
                ADD COLUMN IF NOT EXISTS "QualificationStatus" character varying(50) NULL;
            ALTER TABLE "ContestParticipants"
                ADD COLUMN IF NOT EXISTS "QualificationReason" character varying(250) NULL;
            ALTER TABLE "ContestOfficials"
                ADD COLUMN IF NOT EXISTS "DisplayName" character varying(200) NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BracketMatches_ContestId"
                ON "BracketMatches" ("ContestId")
                WHERE "ContestId" IS NOT NULL;
            CREATE INDEX IF NOT EXISTS "IX_BracketMatches_NextMatchId"
                ON "BracketMatches" ("NextMatchId");
            CREATE INDEX IF NOT EXISTS "IX_BracketMatches_LoserNextMatchId"
                ON "BracketMatches" ("LoserNextMatchId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BracketMatches_SeriesId"
                ON "BracketMatches" ("SeriesId")
                WHERE "SeriesId" IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Contests_SeriesId_SeriesGameNumber"
                ON "Contests" ("SeriesId", "SeriesGameNumber")
                WHERE "SeriesId" IS NOT NULL;

            CREATE TABLE IF NOT EXISTS "BracketAdvancementOverrides" (
                "Id" uuid NOT NULL,
                "BracketMatchId" uuid NOT NULL,
                "WinnerId" uuid NOT NULL,
                "Reason" character varying(500) NOT NULL,
                "RequestedBy" uuid NOT NULL,
                "RequestedAt" timestamp with time zone NOT NULL,
                "Status" integer NOT NULL,
                "ReviewedBy" uuid NULL,
                "ReviewedAt" timestamp with time zone NULL,
                "ReviewNotes" character varying(500) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_BracketAdvancementOverrides" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_BracketAdvancementOverrides_BracketMatchId"
                ON "BracketAdvancementOverrides" ("BracketMatchId");
            CREATE INDEX IF NOT EXISTS "IX_BracketAdvancementOverrides_BracketMatchId_Status"
                ON "BracketAdvancementOverrides" ("BracketMatchId", "Status");
            """);
    }

    private static async Task EnsureGroupQualificationColumnsAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "GroupsCount" integer NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "TeamsPerGroup" integer NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "QualifiersPerGroup" integer NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "BestRunnerUpCount" integer NOT NULL DEFAULT 0;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "QualificationTieBreakers" character varying(300) NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "QualificationAppliedAt" timestamp with time zone NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "FixtureGenerationSeed" integer NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "FixtureHomeAndAway" boolean NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "FixtureDaysBetweenRounds" integer NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "FixtureVenue" character varying(200) NULL;
            ALTER TABLE "Stages" ADD COLUMN IF NOT EXISTS "FixturesGeneratedAt" timestamp with time zone NULL;
            """);
    }

    private static async Task EnsureSchedulingConstraintTablesAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "ScheduledDurationMinutes" integer NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS "CompetitionSchedulePolicies" (
                "Id" uuid NOT NULL,
                "CompetitionId" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "DefaultDurationMinutes" integer NOT NULL DEFAULT 1,
                "MinimumRestMinutes" integer NOT NULL DEFAULT 0,
                "VenueTurnaroundMinutes" integer NOT NULL DEFAULT 0,
                "WindowStartMinutes" integer NOT NULL DEFAULT 0,
                "WindowEndMinutes" integer NOT NULL DEFAULT 1440,
                "BlackoutDatesJson" text NOT NULL DEFAULT '[]',
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_CompetitionSchedulePolicies" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CompetitionSchedulePolicies_CompetitionId"
                ON "CompetitionSchedulePolicies" ("CompetitionId");

            CREATE TABLE IF NOT EXISTS "ScheduleConflictOverrides" (
                "Id" uuid NOT NULL,
                "CompetitionId" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "ConflictKey" character varying(150) NOT NULL,
                "Reason" character varying(500) NOT NULL,
                "RequestedBy" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ScheduleConflictOverrides" PRIMARY KEY ("Id")
            );
            ALTER TABLE "ScheduleConflictOverrides"
                ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ScheduleConflictOverrides_CompetitionId_ConflictKey"
                ON "ScheduleConflictOverrides" ("CompetitionId", "ConflictKey");

            CREATE TABLE IF NOT EXISTS "FixtureAdjustments" (
                "Id" uuid NOT NULL,
                "CompetitionId" uuid NOT NULL,
                "ContestId" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "Venue" character varying(200) NOT NULL,
                "StartTime" timestamp with time zone NOT NULL,
                "DurationMinutes" integer NOT NULL,
                "Reason" character varying(500) NOT NULL,
                "RequestedBy" uuid NOT NULL,
                "RequestedAt" timestamp with time zone NOT NULL,
                "Status" integer NOT NULL,
                "ReviewedBy" uuid NULL,
                "ReviewedAt" timestamp with time zone NULL,
                "ReviewNotes" character varying(500) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_FixtureAdjustments" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_FixtureAdjustments_CompetitionId_RequestedAt"
                ON "FixtureAdjustments" ("CompetitionId", "RequestedAt");
            CREATE INDEX IF NOT EXISTS "IX_FixtureAdjustments_ContestId_Status"
                ON "FixtureAdjustments" ("ContestId", "Status");
            """);
    }

    private static async Task EnsureContestLifecyclePolicyTableAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "ContestLifecyclePolicies" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "SportId" uuid NOT NULL,
                "AllowedTransitionsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ContestLifecyclePolicies" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ContestLifecyclePolicies_TenantId_SportId"
                ON "ContestLifecyclePolicies" ("TenantId", "SportId");
            """);
    }

    private static async Task EnsureContestCheckInTablesAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "ContestOfficials"
                ADD COLUMN IF NOT EXISTS "IsConfirmed" boolean NOT NULL DEFAULT false;

            CREATE TABLE IF NOT EXISTS "ContestReadiness" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "ContestId" uuid NOT NULL,
                "RequireParticipantCheckIn" boolean NOT NULL,
                "RequireRosterConfirmation" boolean NOT NULL,
                "RequireOfficialConfirmation" boolean NOT NULL,
                "RequireVenueConfirmation" boolean NOT NULL,
                "RosterConfirmed" boolean NOT NULL,
                "VenueConfirmed" boolean NOT NULL,
                "OverrideBy" uuid NULL,
                "OverrideReason" character varying(500) NULL,
                "OverriddenAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ContestReadiness" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ContestReadiness_ContestId"
                ON "ContestReadiness" ("ContestId");
            CREATE INDEX IF NOT EXISTS "IX_ContestReadiness_TenantId"
                ON "ContestReadiness" ("TenantId");

            CREATE TABLE IF NOT EXISTS "ContestCheckInHistory" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "ContestId" uuid NOT NULL,
                "Action" character varying(100) NOT NULL,
                "SubjectType" character varying(50) NOT NULL,
                "SubjectId" uuid NULL,
                "PreviousValue" character varying(100) NULL,
                "NewValue" character varying(100) NULL,
                "ActorId" uuid NOT NULL,
                "Reason" character varying(500) NULL,
                "IsOverride" boolean NOT NULL,
                "OccurredAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ContestCheckInHistory" PRIMARY KEY ("Id")
            );
            ALTER TABLE "ContestCheckInHistory"
                ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
            ALTER TABLE "ContestCheckInHistory"
                ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;
            CREATE INDEX IF NOT EXISTS "IX_ContestCheckInHistory_ContestId_OccurredAt"
                ON "ContestCheckInHistory" ("ContestId", "OccurredAt");
            CREATE INDEX IF NOT EXISTS "IX_ContestCheckInHistory_TenantId"
                ON "ContestCheckInHistory" ("TenantId");
            """);
    }

    private static async Task EnsureParticipantAbstractionSchemaAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Contests"
                ADD COLUMN IF NOT EXISTS "AllowedParticipantTypes" character varying(300)
                NOT NULL DEFAULT 'Team|Athlete|Pair|RelayTeam|Crew|Club|School|Country|CompositeTeam';

            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "CompetitionCategory" character varying(100) NULL;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "EmergencyContactName" character varying(200) NULL;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "EmergencyContactRelationship" character varying(100) NULL;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "EmergencyContactPhone" character varying(50) NULL;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "MedicalClearanceStatus" character varying(30)
                NOT NULL DEFAULT 'NotProvided';
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "MedicalClearanceExpiresAt" timestamp with time zone NULL;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "IdentificationDocumentsJson" jsonb
                NOT NULL DEFAULT '[]'::jsonb;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "ContactEmail" character varying(254) NULL;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "ContactPhone" character varying(50) NULL;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "Athlete_ContactEmail" character varying(254) NULL;
            ALTER TABLE "Participants"
                ADD COLUMN IF NOT EXISTS "Athlete_ContactPhone" character varying(50) NULL;
            UPDATE "Participants"
                SET "Athlete_ContactEmail" = COALESCE("Athlete_ContactEmail", "ContactEmail"),
                    "Athlete_ContactPhone" = COALESCE("Athlete_ContactPhone", "ContactPhone")
                WHERE "Type" = 'Athlete'
                  AND ("Athlete_ContactEmail" IS NULL OR "Athlete_ContactPhone" IS NULL);
            UPDATE "Participants"
                SET "MedicalClearanceStatus" = 'Cleared'
                WHERE "MedicalClearance" = TRUE
                  AND "MedicalClearanceStatus" = 'NotProvided';

            ALTER TABLE "TeamRoster"
                ADD COLUMN IF NOT EXISTS "Season" character varying(100) NOT NULL DEFAULT 'Legacy';
            ALTER TABLE "TeamRoster"
                ADD COLUMN IF NOT EXISTS "StartsAt" timestamp with time zone NULL;
            ALTER TABLE "TeamRoster"
                ADD COLUMN IF NOT EXISTS "EndsAt" timestamp with time zone NULL;
            UPDATE "TeamRoster" SET "Season" = "Name" WHERE "Season" = 'Legacy';
            CREATE INDEX IF NOT EXISTS "IX_TeamRosters_TeamId_Season"
                ON "TeamRoster" ("TeamId", "Season");

            ALTER TABLE "RosterEntry"
                ADD COLUMN IF NOT EXISTS "JoinedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
            ALTER TABLE "RosterEntry"
                ADD COLUMN IF NOT EXISTS "LeftAt" timestamp with time zone NULL;
            ALTER TABLE "RosterEntry"
                ADD COLUMN IF NOT EXISTS "MovementReason" character varying(500) NULL;
            CREATE INDEX IF NOT EXISTS "IX_RosterEntries_RosterId_AthleteId"
                ON "RosterEntry" ("RosterId", "AthleteId");

            CREATE TABLE IF NOT EXISTS "ParticipantSportProfiles" (
                "Id" uuid NOT NULL,
                "ParticipantId" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "SportId" uuid NOT NULL,
                "AttributesJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ParticipantSportProfiles" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ParticipantSportProfiles_Participants_ParticipantId"
                    FOREIGN KEY ("ParticipantId") REFERENCES "Participants" ("Id") ON DELETE CASCADE
            );
            ALTER TABLE "ParticipantSportProfiles"
                ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
            ALTER TABLE "ParticipantSportProfiles"
                ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ParticipantSportProfiles_ParticipantId_SportId"
                ON "ParticipantSportProfiles" ("ParticipantId", "SportId");
            CREATE INDEX IF NOT EXISTS "IX_ParticipantSportProfiles_TenantId"
                ON "ParticipantSportProfiles" ("TenantId");
            """);
    }

    private static async Task EnsureUniversalContestSchemaAsync(
        SportsPlatformDbContext context)
    {
        if (!context.Database.IsNpgsql()) return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "ContestMetrics" (
                "Id" uuid NOT NULL,
                "ContestId" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "ParticipantId" uuid NULL,
                "SegmentId" uuid NULL,
                "Name" character varying(100) NOT NULL,
                "NumericValue" numeric(18,6) NULL,
                "TextValue" character varying(1000) NULL,
                "Unit" character varying(50) NULL,
                "DataType" character varying(32) NOT NULL DEFAULT 'Decimal',
                "Precision" integer NOT NULL DEFAULT 6,
                "RecordedAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ContestMetrics" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ContestMetrics_Contests_ContestId"
                    FOREIGN KEY ("ContestId") REFERENCES "Contests" ("Id") ON DELETE CASCADE
            );
            ALTER TABLE "ContestMetrics"
                ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
            ALTER TABLE "ContestMetrics"
                ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;
            ALTER TABLE "ContestMetrics"
                ADD COLUMN IF NOT EXISTS "DataType" character varying(32) NOT NULL DEFAULT 'Decimal';
            ALTER TABLE "ContestMetrics"
                ADD COLUMN IF NOT EXISTS "Precision" integer NOT NULL DEFAULT 6;
            CREATE INDEX IF NOT EXISTS "IX_ContestMetrics_ContestId_Name"
                ON "ContestMetrics" ("ContestId", "Name");
            CREATE INDEX IF NOT EXISTS "IX_ContestMetrics_TenantId"
                ON "ContestMetrics" ("TenantId");
            """);
    }
}
