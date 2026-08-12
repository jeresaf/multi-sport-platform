using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Branding.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("TenantBrandings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.TenantName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.ShortName)
            .HasMaxLength(100);

        builder.Property(b => b.LogoUrl)
            .HasMaxLength(500);

        builder.Property(b => b.PrimaryColor)
            .HasMaxLength(20);

        builder.Property(b => b.SecondaryColor)
            .HasMaxLength(20);

        builder.Property(b => b.AccentColor)
            .HasMaxLength(20);

        builder.Property(b => b.PublicSlug)
            .HasMaxLength(100);

        builder.Property(b => b.ContactEmail)
            .HasMaxLength(200);

        builder.Property(b => b.ContactPhone)
            .HasMaxLength(50);

        builder.Property(b => b.Website)
            .HasMaxLength(500);

        builder.Property(b => b.TenantId).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt);

        builder.HasIndex(b => b.TenantId).IsUnique();
        builder.HasIndex(b => b.PublicSlug).IsUnique();
    }
}