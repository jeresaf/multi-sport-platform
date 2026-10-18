namespace SportsPlatform.BuildingBlocks.Privacy;

/// <summary>How personal a value is (ADR-0015, docs/architecture/data-conventions.md).</summary>
public enum PersonalDataClass
{
    /// <summary>Shown on public sites, e.g. a display name.</summary>
    Public,

    /// <summary>Admin APIs only, e.g. a full legal name.</summary>
    Internal,

    /// <summary>Admin APIs with an explicit permission; never in logs, analytics or audit metadata.</summary>
    Private,

    /// <summary>Encrypted at rest, never exported by default; never in logs, analytics or audit metadata.</summary>
    Sensitive,
}

/// <summary>
/// Classifies a property of a DTO or audit metadata record. Values of <see cref="PersonalDataClass.Private"/> and
/// <see cref="PersonalDataClass.Sensitive"/> properties are masked where data leaves its owner (audit metadata).
/// Database columns are classified in their EF configuration instead. On a positional record, target the property:
/// <c>record Metadata([property: PersonalData(PersonalDataClass.Private)] string Email)</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class PersonalDataAttribute(PersonalDataClass classification) : Attribute
{
    public PersonalDataClass Classification { get; } = classification;

    public bool IsMasked => Classification is PersonalDataClass.Private or PersonalDataClass.Sensitive;
}
