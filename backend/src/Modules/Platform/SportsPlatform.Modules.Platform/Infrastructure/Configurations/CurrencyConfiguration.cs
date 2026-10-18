using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Platform.Domain.Reference;

namespace SportsPlatform.Modules.Platform.Infrastructure.Configurations;

internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable(ReferenceTables.Currencies, table =>
        {
            table.HasCheckConstraint("ck_currencies_code", "code ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("ck_currencies_numeric_code", "numeric_code ~ '^[0-9]{3}$'");
            table.HasCheckConstraint("ck_currencies_minor_units", "minor_units BETWEEN 0 AND 6");
        });
        ReferenceTables.ConfigureCode(builder, 3);
        builder.Property(c => c.NumericCode).HasMaxLength(3);
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.MinorUnits).HasColumnType("smallint");
        // Among active rows only: ISO may give a retired currency's number to a new one.
        builder.HasIndex(c => c.NumericCode).IsUnique().HasFilter("is_active");
    }
}
