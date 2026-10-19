using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;

namespace SportsPlatform.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// <c>identity.data_protection_keys</c> (S03 D14): the key ring for cookies and Identity's tokens. The repository only
/// ever inserts (a revocation is a new element), so the table is append-only.
/// </summary>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("data_protection_keys");
        builder.IsAppendOnly();
        builder.HasKey(k => k.Id);
    }
}
