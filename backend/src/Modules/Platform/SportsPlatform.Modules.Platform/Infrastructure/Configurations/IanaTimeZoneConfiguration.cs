using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Platform.Domain.Reference;

namespace SportsPlatform.Modules.Platform.Infrastructure.Configurations;

internal sealed class IanaTimeZoneConfiguration : IEntityTypeConfiguration<IanaTimeZone>
{
    public void Configure(EntityTypeBuilder<IanaTimeZone> builder)
    {
        // No foreign key to countries: the seed order doesn't matter, and a test checks the data instead.
        builder.ToTable(ReferenceTables.TimeZones, table =>
            table.HasCheckConstraint("ck_time_zones_country_code", "country_code ~ '^[A-Z]{2}$'"));
        ReferenceTables.ConfigureCode(builder, 64);
        builder.Property(z => z.CountryCode).HasMaxLength(2);
        builder.HasIndex(z => z.CountryCode);
    }
}
