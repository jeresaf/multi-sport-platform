using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Platform.Domain.Reference;

namespace SportsPlatform.Modules.Platform.Infrastructure.Configurations;

/// <summary>
/// The reference tables: read-only for the runtime roles (migration <c>ReadOnlyForRuntimeRoles</c>), written by the
/// REFERENCE seeders as the owner. Codes are checked by the database too, so a bad data file fails the seed.
/// </summary>
internal static class ReferenceTables
{
    public const string Countries = "countries";
    public const string Currencies = "currencies";
    public const string Languages = "languages";
    public const string TimeZones = "time_zones";

    public static void ConfigureCode<T>(EntityTypeBuilder<T> builder, int maxLength)
        where T : ReferenceEntry
    {
        builder.Property(e => e.Code).HasMaxLength(maxLength);
        builder.HasIndex(e => e.Code).IsUnique();
    }
}
