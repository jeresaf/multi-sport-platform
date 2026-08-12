using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Sponsorship.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class SponsorConfiguration : IEntityTypeConfiguration<Sponsor>
{
    public void Configure(EntityTypeBuilder<Sponsor> builder)
    {
        builder.ToTable("Sponsors");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.LogoUrl).HasMaxLength(2048);
        builder.Property(item => item.Website).HasMaxLength(2048);
        builder.Property(item => item.ContactEmail).HasMaxLength(320);
        builder.Property(item => item.ContactPhone).HasMaxLength(50);
        builder.HasIndex(item => new { item.TenantId, item.Name });
        builder.HasIndex(item => new { item.TenantId, item.IsActive });
    }
}

public sealed class SponsorshipConfiguration : IEntityTypeConfiguration<Sponsorship>
{
    public void Configure(EntityTypeBuilder<Sponsorship> builder)
    {
        builder.ToTable("Sponsorships");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.Currency).IsRequired().HasMaxLength(3);
        builder.Property(item => item.Benefits).HasMaxLength(4000);
        builder.Property(item => item.ContractUrl).HasMaxLength(2048);
        builder.Property(item => item.Placement).IsRequired().HasMaxLength(50);
        builder.Property(item => item.Value).HasPrecision(18, 2);
        builder.HasOne<Sponsor>().WithMany().HasForeignKey(item => item.SponsorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Competition>().WithMany().HasForeignKey(item => item.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Contest>().WithMany().HasForeignKey(item => item.ContestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TenantId, item.Status, item.StartDate, item.EndDate });
        builder.HasIndex(item => new { item.TenantId, item.Placement, item.DisplayOrder });
    }
}
