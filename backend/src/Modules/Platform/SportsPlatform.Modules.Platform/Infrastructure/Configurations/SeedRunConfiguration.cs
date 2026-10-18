using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Platform.Domain.Seeding;

namespace SportsPlatform.Modules.Platform.Infrastructure.Configurations;

/// <summary><c>platform.seed_runs</c>: append-only (migration <c>AppendOnly</c>), category as its UPPER_SNAKE code.</summary>
internal sealed class SeedRunConfiguration : IEntityTypeConfiguration<SeedRun>
{
    public const string Table = "seed_runs";

    public void Configure(EntityTypeBuilder<SeedRun> builder)
    {
        builder.ToTable(Table, table => table.HasCheckConstraint(
            "ck_seed_runs_category", "category IN ('REFERENCE', 'CONFIGURATION', 'DEMO', 'TEST')"));
        builder.IsAppendOnly();

        builder.Property(r => r.SeederKey).HasMaxLength(100);
        builder.Property(r => r.Category)
            .HasConversion(value => value.Code(), value => Enum.Parse<SeedCategory>(value, true))
            .HasMaxLength(20);
        builder.Property(r => r.Checksum).HasMaxLength(100);
        builder.Property(r => r.Environment).HasMaxLength(50);

        // The runner reads a seeder's latest run.
        builder.HasIndex(r => new { r.SeederKey, r.Id });
    }
}
