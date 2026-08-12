using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Subscriptions.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Plan)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.StartDate)
            .IsRequired();

        builder.Property(s => s.EndDate)
            .IsRequired();

        builder.Property(s => s.GracePeriodEnd);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(s => s.AutoRenew)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.FeatureEntitlements)
            .HasColumnType("jsonb");

        builder.Property(s => s.UsageLimits)
            .HasColumnType("jsonb");

        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        builder.HasIndex(s => s.TenantId).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}