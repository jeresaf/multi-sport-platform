using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    public void Configure(EntityTypeBuilder<Sport> builder)
    {
        builder.ToTable("Sports");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.Code).IsUnique();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.Icon)
            .HasMaxLength(200);

        builder.Property(s => s.SupportedParticipantShapes)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(s => s.CurrentVersion)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt);

        // SportDefinitionVersion as owned entity
        builder.OwnsMany(s => s.Versions, v =>
        {
            v.WithOwner().HasForeignKey("SportId");
            v.ToTable("SportDefinitionVersions");
            v.HasKey(v => v.Id);

            v.Property(v => v.VersionNumber).IsRequired();
            v.Property(v => v.Name).IsRequired().HasMaxLength(200);
            v.Property(v => v.CreatedBy).IsRequired().HasMaxLength(200);
            v.Property(v => v.ContestShape).HasMaxLength(100);
            v.Property(v => v.ParticipantType).HasMaxLength(100);
            v.Property(v => v.SegmentDefinitions).HasColumnType("jsonb");
            v.Property(v => v.EventDefinitions).HasColumnType("jsonb");
            v.Property(v => v.MetricDefinitions).HasColumnType("jsonb");
            v.Property(v => v.ResultMethods).HasColumnType("jsonb");
            v.Property(v => v.RankingRules).HasColumnType("jsonb");
            v.Property(v => v.DisplayConfiguration).HasColumnType("jsonb");
            v.Property(v => v.ValidationConfiguration).HasColumnType("jsonb");
            v.Property(v => v.CreatedAt).IsRequired();
            v.Property(v => v.PublishedAt);
            v.HasIndex("SportId", nameof(SportDefinitionVersion.VersionNumber)).IsUnique();
        });
    }
}

public class TenantSportConfiguration : IEntityTypeConfiguration<TenantSport>
{
    public void Configure(EntityTypeBuilder<TenantSport> builder)
    {
        builder.ToTable("TenantSports");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.SportId }).IsUnique();
        builder.Property(item => item.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.HasOne<Sport>().WithMany().HasForeignKey(item => item.SportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SportsPlatform.Modules.Tenancy.Entities.Tenant>()
            .WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
