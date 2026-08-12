using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Scheduling.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class CompetitionSchedulePolicyConfiguration
    : IEntityTypeConfiguration<CompetitionSchedulePolicy>
{
    public void Configure(EntityTypeBuilder<CompetitionSchedulePolicy> builder)
    {
        builder.ToTable("CompetitionSchedulePolicies");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.CompetitionId).IsRequired();
        builder.Property(item => item.BlackoutDatesJson).IsRequired().HasColumnType("text");
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.HasIndex(item => item.CompetitionId).IsUnique();
    }
}

public sealed class ScheduleConflictOverrideConfiguration
    : IEntityTypeConfiguration<ScheduleConflictOverride>
{
    public void Configure(EntityTypeBuilder<ScheduleConflictOverride> builder)
    {
        builder.ToTable("ScheduleConflictOverrides");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.CompetitionId).IsRequired();
        builder.Property(item => item.ConflictKey).IsRequired().HasMaxLength(150);
        builder.Property(item => item.Reason).IsRequired().HasMaxLength(500);
        builder.Property(item => item.RequestedBy).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.HasIndex(item => new { item.CompetitionId, item.ConflictKey }).IsUnique();
    }
}

public sealed class FixtureAdjustmentConfiguration : IEntityTypeConfiguration<FixtureAdjustment>
{
    public void Configure(EntityTypeBuilder<FixtureAdjustment> builder)
    {
        builder.ToTable("FixtureAdjustments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.Venue).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Reason).IsRequired().HasMaxLength(500);
        builder.Property(item => item.ReviewNotes).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<int>();
        builder.HasIndex(item => new { item.CompetitionId, item.RequestedAt });
        builder.HasIndex(item => new { item.ContestId, item.Status });
    }
}
