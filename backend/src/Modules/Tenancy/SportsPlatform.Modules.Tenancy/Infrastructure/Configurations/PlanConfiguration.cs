using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Tenancy.Domain.Plans;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>
/// <c>tenancy.plans</c> and <c>tenancy.plan_entitlements</c>: configuration data (S04 D2), global (no <c>tenant_id</c>,
/// no RLS), read-only for the runtime roles (migration <c>ReadOnlyForRuntimeRoles</c>), written by
/// <c>tenancy.configuration.plans</c> as the owner. Rank order is checked at startup (EntitlementRules), not here: the
/// seeder may swap two ranks in one save.
/// </summary>
internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public const string Table = "plans";

    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_plans_code", PlanTables.CodeCheck("code"));
            table.HasCheckConstraint("ck_plans_rank", "rank >= 1");
        });

        builder.Property(p => p.Code).HasMaxLength(PlanTables.CodeMaxLength);
        builder.Property(p => p.Name).HasMaxLength(100);
        builder.HasIndex(p => p.Code).IsUnique();

        builder.HasMany(p => p.Entitlements).WithOne().HasForeignKey(e => e.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Entitlements).HasField("_entitlements");
    }
}

internal sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
{
    public const string Table = "plan_entitlements";

    public void Configure(EntityTypeBuilder<PlanEntitlement> builder)
    {
        // value: jsonb through EntitlementValueConverter (TenancyDbContext's conventions).
        builder.ToTable(Table, table => table.HasCheckConstraint("ck_plan_entitlements_entitlement_code", PlanTables.CodeCheck("entitlement_code")));

        builder.Property(e => e.EntitlementCode).HasMaxLength(PlanTables.CodeMaxLength);
        builder.HasIndex(e => new { e.PlanId, e.EntitlementCode }).IsUnique();
    }
}

internal static class PlanTables
{
    public const int CodeMaxLength = 50;

    public static string CodeCheck(string column) => $"{column} ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'";
}
