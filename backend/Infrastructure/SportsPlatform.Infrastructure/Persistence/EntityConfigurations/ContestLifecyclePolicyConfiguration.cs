using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class ContestLifecyclePolicyConfiguration
    : IEntityTypeConfiguration<ContestLifecyclePolicy>
{
    public void Configure(EntityTypeBuilder<ContestLifecyclePolicy> builder)
    {
        builder.ToTable("ContestLifecyclePolicies");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.SportId).IsRequired();
        builder.Property(item => item.AllowedTransitionsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.UpdatedAt);
        builder.HasIndex(item => new { item.TenantId, item.SportId }).IsUnique();
    }
}
