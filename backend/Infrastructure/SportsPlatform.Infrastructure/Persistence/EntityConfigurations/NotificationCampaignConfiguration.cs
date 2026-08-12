using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Notifications.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class NotificationCampaignConfiguration
    : IEntityTypeConfiguration<NotificationCampaign>
{
    public void Configure(EntityTypeBuilder<NotificationCampaign> builder)
    {
        builder.ToTable("NotificationCampaigns");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.Type).HasConversion<int>();
        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.Title).IsRequired().HasMaxLength(160);
        builder.Property(item => item.Body).IsRequired().HasMaxLength(1000);
        builder.Property(item => item.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.Property(item => item.Reason).IsRequired().HasMaxLength(500);
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CreatedAt });
    }
}
