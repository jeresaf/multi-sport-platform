using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>
/// <c>tenancy.subscription_periods</c>: tenant-owned (RLS), read per tenant by start date. A row changes only when it
/// is cancelled (the three <c>cancel</c> columns are set together). The plan code names a row of
/// <c>tenancy.plans</c> without a foreign key: plans are configuration, and a tenant's history outlives a dropped plan.
/// Overlaps are checked under the tenant's subscription lock, not by a constraint.
/// </summary>
internal sealed class SubscriptionPeriodConfiguration : IEntityTypeConfiguration<SubscriptionPeriod>
{
    public const string Table = "subscription_periods";

    public void Configure(EntityTypeBuilder<SubscriptionPeriod> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_subscription_periods_kind", $"kind IN ({StatusValues.SqlList<SubscriptionKind>()})");
            table.HasCheckConstraint("ck_subscription_periods_dates", "ends_on >= starts_on");
            table.HasCheckConstraint(
                "ck_subscription_periods_expiring_window_days",
                $"expiring_window_days BETWEEN 0 AND {SubscriptionPeriod.MaxWindowDays}");
            table.HasCheckConstraint("ck_subscription_periods_grace_days", $"grace_days BETWEEN 0 AND {SubscriptionPeriod.MaxWindowDays}");
            table.HasCheckConstraint(
                "ck_subscription_periods_cancelled",
                "(cancelled_at IS NULL AND cancelled_by IS NULL AND cancel_reason IS NULL) " +
                "OR (cancelled_at IS NOT NULL AND cancelled_by IS NOT NULL AND cancel_reason IS NOT NULL)");
        });

        builder.Property(p => p.PlanCode).HasMaxLength(SubscriptionPeriod.PlanCodeMaxLength);
        builder.Property(p => p.Kind).HasConversion(StatusValues.UpperSnake<SubscriptionKind>()).HasMaxLength(20);
        builder.Property(p => p.CancelReason).HasMaxLength(SubscriptionPeriod.CancelReasonMaxLength);

        builder.HasIndex(p => new { p.TenantId, p.StartsOn });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
