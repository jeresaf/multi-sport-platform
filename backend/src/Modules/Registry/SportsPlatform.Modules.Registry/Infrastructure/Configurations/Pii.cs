using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// The personal-data class of a column (ADR-0015; docs/architecture/data-conventions.md, Personal-data classification),
/// as the annotation <c>pii</c>: <c>PUBLIC</c>, <c>INTERNAL</c>, <c>PRIVATE</c> or <c>SENSITIVE</c>.
/// </summary>
public static class Pii
{
    public const string Annotation = "pii";

    public static PropertyBuilder<TProperty> IsPersonalData<TProperty>(
        this PropertyBuilder<TProperty> property, PersonalDataClass classification)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasAnnotation(Annotation, Name(classification));
    }

    public static string Name(PersonalDataClass classification) => classification switch
    {
        PersonalDataClass.Public => "PUBLIC",
        PersonalDataClass.Internal => "INTERNAL",
        PersonalDataClass.Private => "PRIVATE",
        PersonalDataClass.Sensitive => "SENSITIVE",
        _ => throw new ArgumentOutOfRangeException(nameof(classification)),
    };
}
