using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class ContestStatusHistoryConfiguration
    : IEntityTypeConfiguration<ContestStatusHistory>
{
    public void Configure(EntityTypeBuilder<ContestStatusHistory> builder)
    {
        builder.ToTable("ContestStatusHistory");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.ContestId).IsRequired();
        builder.Property(item => item.FromStatus).IsRequired().HasMaxLength(50)
            .HasConversion<string>();
        builder.Property(item => item.ToStatus).IsRequired().HasMaxLength(50)
            .HasConversion<string>();
        builder.Property(item => item.PhaseCode).HasMaxLength(100);
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.Property(item => item.OccurredAt).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.HasOne<Contest>().WithMany().HasForeignKey(item => item.ContestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.ContestId, item.OccurredAt });
        builder.HasIndex(item => item.TenantId);
    }
}
