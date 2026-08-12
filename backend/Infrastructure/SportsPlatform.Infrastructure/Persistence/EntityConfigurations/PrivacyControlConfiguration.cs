using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.Property(item => item.PolicyVersion).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Source).HasMaxLength(80).IsRequired();
        builder.Property(item => item.IpAddress).HasMaxLength(50);
        builder.HasIndex(item => new { item.UserId, item.Kind, item.OccurredAt });
        builder.HasIndex(item => new { item.TenantId, item.OccurredAt });
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PrivacyControlSettingsConfiguration : IEntityTypeConfiguration<PrivacyControlSettings>
{
    public void Configure(EntityTypeBuilder<PrivacyControlSettings> builder)
    {
        builder.HasIndex(item => item.UserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PrivacyActionRequestConfiguration : IEntityTypeConfiguration<PrivacyActionRequest>
{
    public void Configure(EntityTypeBuilder<PrivacyActionRequest> builder)
    {
        builder.Property(item => item.Details).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.Response).HasMaxLength(2000);
        builder.HasIndex(item => new { item.TenantId, item.Status, item.RequestedAt });
        builder.HasIndex(item => new { item.UserId, item.RequestedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TenantRetentionPolicyConfiguration : IEntityTypeConfiguration<TenantRetentionPolicy>
{
    public void Configure(EntityTypeBuilder<TenantRetentionPolicy> builder)
    {
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.Resource }).IsUnique();
        builder.HasIndex(item => new { item.IsActive, item.NextRunAt });
    }
}
