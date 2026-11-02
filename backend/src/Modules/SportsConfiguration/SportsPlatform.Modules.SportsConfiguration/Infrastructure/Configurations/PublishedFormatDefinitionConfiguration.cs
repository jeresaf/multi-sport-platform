using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.SportsConfiguration.Domain.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Formats;

namespace SportsPlatform.Modules.SportsConfiguration.Infrastructure.Configurations;

/// <summary>
/// <c>config.format_definitions</c>: configuration data (S05 D4, D10), global (no <c>tenant_id</c>, no RLS), read-only for
/// the runtime roles (migration <c>ReadOnlyForRuntimeRoles</c>), written by <c>config.configuration.formats</c> as the owner.
/// </summary>
internal sealed class PublishedFormatDefinitionConfiguration : IEntityTypeConfiguration<PublishedFormatDefinition>
{
    public const string Table = "format_definitions";

    public void Configure(EntityTypeBuilder<PublishedFormatDefinition> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_format_definitions_code", ConfigurationTables.CodeCheck("code"));
            table.HasCheckConstraint("ck_format_definitions_version", "version >= 1");
            table.HasCheckConstraint("ck_format_definitions_status", $"status IN ({EnumColumns.SqlList<DefinitionStatus>()})");
            table.HasCheckConstraint("ck_format_definitions_content_hash", ConfigurationTables.HashCheck("content_hash"));
        });

        builder.Property(d => d.Code).HasMaxLength(ConfigurationTables.CodeMaxLength);
        builder.Property(d => d.Name).HasMaxLength(100);
        builder.Property(d => d.Status).HasConversion(EnumColumns.UpperSnake<DefinitionStatus>()).HasMaxLength(20);
        builder.Property(d => d.Content).HasColumnType("jsonb");
        builder.Property(d => d.ContentHash).HasMaxLength(ConfigurationTables.HashMaxLength);
        builder.HasIndex(d => new { d.Code, d.Version }).IsUnique();
    }
}
