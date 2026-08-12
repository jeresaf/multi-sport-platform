using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.LiveScoring.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class ContestReadinessConfiguration : IEntityTypeConfiguration<ContestReadiness>
{
    public void Configure(EntityTypeBuilder<ContestReadiness> builder)
    {
        builder.ToTable("ContestReadiness");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OverrideReason).HasMaxLength(500);
        builder.HasIndex(item => item.ContestId).IsUnique();
        builder.HasIndex(item => item.TenantId);
    }
}

public sealed class ContestCheckInHistoryConfiguration : IEntityTypeConfiguration<ContestCheckInHistory>
{
    public void Configure(EntityTypeBuilder<ContestCheckInHistory> builder)
    {
        builder.ToTable("ContestCheckInHistory");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Action).IsRequired().HasMaxLength(100);
        builder.Property(item => item.SubjectType).IsRequired().HasMaxLength(50);
        builder.Property(item => item.PreviousValue).HasMaxLength(100);
        builder.Property(item => item.NewValue).HasMaxLength(100);
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.HasIndex(item => new { item.ContestId, item.OccurredAt });
        builder.HasIndex(item => item.TenantId);
    }
}
