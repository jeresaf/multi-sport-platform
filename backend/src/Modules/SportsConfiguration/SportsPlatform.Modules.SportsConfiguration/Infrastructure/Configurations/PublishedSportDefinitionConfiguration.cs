using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Domain.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Infrastructure.Configurations;

/// <summary>
/// <c>config.sport_definitions</c>: configuration data (S05 D4), global (no <c>tenant_id</c>, no RLS), read-only for the
/// runtime roles (migration <c>ReadOnlyForRuntimeRoles</c>), written by <c>config.configuration.sports</c> as the owner.
/// </summary>
internal sealed class PublishedSportDefinitionConfiguration : IEntityTypeConfiguration<PublishedSportDefinition>
{
    public const string Table = "sport_definitions";

    public void Configure(EntityTypeBuilder<PublishedSportDefinition> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_sport_definitions_variant", ConfigurationTables.CodeCheck("variant"));
            table.HasCheckConstraint("ck_sport_definitions_sport", ConfigurationTables.CodeCheck("sport"));
            table.HasCheckConstraint("ck_sport_definitions_scoring_family", ConfigurationTables.CodeCheck("scoring_family"));
            table.HasCheckConstraint("ck_sport_definitions_version", "version >= 1");
            table.HasCheckConstraint("ck_sport_definitions_shape", $"shape IN ({EnumColumns.SqlList<ContestShape>()})");
            table.HasCheckConstraint("ck_sport_definitions_status", $"status IN ({EnumColumns.SqlList<DefinitionStatus>()})");
            table.HasCheckConstraint("ck_sport_definitions_content_hash", ConfigurationTables.HashCheck("content_hash"));
        });

        builder.Property(d => d.Variant).HasMaxLength(ConfigurationTables.CodeMaxLength);
        builder.Property(d => d.Sport).HasMaxLength(ConfigurationTables.CodeMaxLength);
        builder.Property(d => d.Name).HasMaxLength(100);
        builder.Property(d => d.ScoringFamily).HasMaxLength(ConfigurationTables.CodeMaxLength);
        builder.Property(d => d.Shape).HasConversion(EnumColumns.UpperSnake<ContestShape>()).HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion(EnumColumns.UpperSnake<DefinitionStatus>()).HasMaxLength(20);
        builder.Property(d => d.Content).HasColumnType("jsonb");
        builder.Property(d => d.ContentHash).HasMaxLength(ConfigurationTables.HashMaxLength);
        builder.HasIndex(d => new { d.Variant, d.Version }).IsUnique();
    }
}

internal static class ConfigurationTables
{
    public const int CodeMaxLength = 50;

    /// <summary><c>sha256:</c> and 64 hex digits.</summary>
    public const int HashMaxLength = 71;

    public static string CodeCheck(string column) => $"{column} ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'";

    public static string HashCheck(string column) => $"{column} ~ '^sha256:[0-9a-f]{{64}}$'";
}
