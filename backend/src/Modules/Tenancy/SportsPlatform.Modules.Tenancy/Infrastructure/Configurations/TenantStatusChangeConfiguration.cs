using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary><c>tenancy.tenant_status_history</c>: append-only (migration <c>AppendOnly</c>), read per tenant in time order.</summary>
internal sealed class TenantStatusChangeConfiguration : IEntityTypeConfiguration<TenantStatusChange>
{
    public const string Table = "tenant_status_history";

    public void Configure(EntityTypeBuilder<TenantStatusChange> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_tenant_status_history_from_status", $"from_status IN ({StatusValues.TenantStatuses})");
            table.HasCheckConstraint("ck_tenant_status_history_to_status", $"to_status IN ({StatusValues.TenantStatuses})");
            table.HasCheckConstraint("ck_tenant_status_history_actor_kind", $"actor_kind IN ({StatusValues.SqlList<ActorKind>()})");
        });
        builder.IsAppendOnly();

        builder.Property(c => c.FromStatus).HasConversion(StatusValues.UpperSnake<TenantStatus>()).HasMaxLength(20);
        builder.Property(c => c.ToStatus).HasConversion(StatusValues.UpperSnake<TenantStatus>()).HasMaxLength(20);
        builder.Property(c => c.Reason).HasMaxLength(TenantStatusChange.ReasonMaxLength);
        builder.Property(c => c.ActorKind).HasConversion(StatusValues.UpperSnake<ActorKind>()).HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.OccurredAt });
    }
}
