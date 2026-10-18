using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Platform.Domain.Reference;

namespace SportsPlatform.Modules.Platform.Infrastructure.Configurations;

internal sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable(ReferenceTables.Countries, table =>
        {
            table.HasCheckConstraint("ck_countries_code", "code ~ '^[A-Z]{2}$'");
            table.HasCheckConstraint("ck_countries_alpha3_code", "alpha3_code ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("ck_countries_numeric_code", "numeric_code ~ '^[0-9]{3}$'");
        });
        ReferenceTables.ConfigureCode(builder, 2);
        builder.Property(c => c.Alpha3Code).HasColumnName("alpha3_code").HasMaxLength(3); // The convention says alpha3code.
        builder.Property(c => c.NumericCode).HasMaxLength(3);
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.HasIndex(c => c.Alpha3Code).IsUnique();
        builder.HasIndex(c => c.NumericCode).IsUnique();
    }
}
