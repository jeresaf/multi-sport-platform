using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.SportsConfiguration.Domain.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Rulesets;

namespace SportsPlatform.Modules.SportsConfiguration.Infrastructure.Configurations;

/// <summary>
/// <c>config.ruleset_templates</c>: configuration data (S05 D4, D11), global (no <c>tenant_id</c>, no RLS), read-only for
/// the runtime roles (migration <c>ReadOnlyForRuntimeRoles</c>), written by <c>config.configuration.rulesets</c> as the
/// owner. No foreign key to <c>sport_definitions</c> or <c>format_definitions</c>: the validator checks the references
/// before the seeder writes.
/// </summary>
internal sealed class PublishedRulesetTemplateConfiguration : IEntityTypeConfiguration<PublishedRulesetTemplate>
{
    public const string Table = "ruleset_templates";

    public void Configure(EntityTypeBuilder<PublishedRulesetTemplate> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_ruleset_templates_code", ConfigurationTables.CodeCheck("code"));
            table.HasCheckConstraint("ck_ruleset_templates_version", "version >= 1");
            table.HasCheckConstraint("ck_ruleset_templates_sport_variant", ConfigurationTables.CodeCheck("sport_variant"));
            table.HasCheckConstraint("ck_ruleset_templates_sport_version", "sport_version >= 1");
            table.HasCheckConstraint("ck_ruleset_templates_formats", "cardinality(formats) >= 1");
            table.HasCheckConstraint("ck_ruleset_templates_status", $"status IN ({EnumColumns.SqlList<DefinitionStatus>()})");
            table.HasCheckConstraint("ck_ruleset_templates_content_hash", ConfigurationTables.HashCheck("content_hash"));
        });

        builder.Property(t => t.Code).HasMaxLength(ConfigurationTables.CodeMaxLength);
        builder.Property(t => t.Name).HasMaxLength(100);
        builder.Property(t => t.SportVariant).HasMaxLength(ConfigurationTables.CodeMaxLength);
        builder.Property(t => t.Formats).HasColumnType("text[]");
        builder.Property(t => t.Status).HasConversion(EnumColumns.UpperSnake<DefinitionStatus>()).HasMaxLength(20);
        builder.Property(t => t.Content).HasColumnType("jsonb");
        builder.Property(t => t.ContentHash).HasMaxLength(ConfigurationTables.HashMaxLength);
        builder.HasIndex(t => new { t.Code, t.Version }).IsUnique();
    }
}
