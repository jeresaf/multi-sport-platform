using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Subscriptions.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class TenantUsageCounterConfiguration : IEntityTypeConfiguration<TenantUsageCounter>
{
    public void Configure(EntityTypeBuilder<TenantUsageCounter> builder)
    {
        builder.ToTable("TenantUsageCounters");
        builder.HasKey(counter => counter.Id);
        builder.Property(counter => counter.Metric).IsRequired().HasMaxLength(100);
        builder.Property(counter => counter.PeriodStartUtc).IsRequired();
        builder.Property(counter => counter.Value).IsRequired();
        builder.Property(counter => counter.UpdatedAt).IsRequired();
        builder.HasIndex(counter => new { counter.TenantId, counter.Metric, counter.PeriodStartUtc }).IsUnique();
        builder.HasIndex(counter => counter.TenantId);
    }
}
