using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Registrations.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class CompetitionEligibilityPolicyConfiguration : IEntityTypeConfiguration<CompetitionEligibilityPolicy>
{
    public void Configure(EntityTypeBuilder<CompetitionEligibilityPolicy> builder)
    {
        builder.ToTable("CompetitionEligibilityPolicies");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.CompetitionId).IsRequired();
        builder.Property(item => item.RequiredDocumentTypesJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.MinimumWeightKg).HasPrecision(8, 2);
        builder.Property(item => item.MaximumWeightKg).HasPrecision(8, 2);
        builder.HasIndex(item => item.CompetitionId).IsUnique();
    }
}
