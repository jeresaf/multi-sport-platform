using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Platform.Domain.Reference;

namespace SportsPlatform.Modules.Platform.Infrastructure.Configurations;

internal sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable(ReferenceTables.Languages, table =>
            table.HasCheckConstraint("ck_languages_code", "code ~ '^[a-z]{2}$'"));
        ReferenceTables.ConfigureCode(builder, 2);
        builder.Property(l => l.Name).HasMaxLength(200);
    }
}
