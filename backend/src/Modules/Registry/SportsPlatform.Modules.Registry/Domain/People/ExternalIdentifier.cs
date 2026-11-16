using System.Text.RegularExpressions;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// An identifier another authority gave a registry entity (<c>registry.external_identifiers</c>, tenant-owned, S06 D10;
/// docs/architecture/data-conventions.md, Identifiers), e.g. <c>KMSA_REG_NO</c> or <c>SCHOOL_ADMISSION_NO</c>. A value is
/// unique per tenant, authority and entity type, ended ones included, so it is never reused. The entity is named by
/// type and ID without a foreign key, because one table serves every registry entity; a change to a person's
/// identifiers must mark the person changed (<c>ModuleDbContext.MarkChanged</c>, S06 D11). It never changes the
/// entity's own ID (AR-23). The commands come with S06-T3.
/// </summary>
public sealed partial class ExternalIdentifier : Entity, ITenantOwned
{
    public const string AuthorityPattern = "^[A-Z][A-Z0-9_]{1,39}$";
    public const int AuthorityMaxLength = 40;
    public const int ValueMaxLength = 100;

    private ExternalIdentifier(
        ExternalIdentifierEntityType entityType, Guid entityId, string authority, string value, DateOnly? validFrom)
        : base(NewId())
    {
        EntityType = entityType;
        EntityId = entityId;
        Authority = authority;
        Value = value;
        ValidFrom = validFrom;
    }

    private ExternalIdentifier()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public ExternalIdentifierEntityType EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    /// <summary>Who issued the value: a code chosen by the tenant.</summary>
    public string Authority { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public DateOnly? ValidFrom { get; private set; }

    /// <summary>Set when the identifier is ended; the row stays.</summary>
    public DateOnly? ValidTo { get; private set; }

    /// <summary>An identifier of a person; the use case has validated the authority and the value.</summary>
    public static ExternalIdentifier ForPerson(Guid personId, string authority, string value, DateOnly? validFrom = null)
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException("An identifier belongs to a person.", nameof(personId));
        }

        ArgumentNullException.ThrowIfNull(authority);
        if (!AuthorityRegex().IsMatch(authority))
        {
            throw new ArgumentException("An authority is an UPPER_SNAKE code of 2 to 40 characters.", nameof(authority));
        }

        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > ValueMaxLength)
        {
            throw new ArgumentException($"An identifier's value has 1 to {ValueMaxLength} characters.", nameof(value));
        }

        return new ExternalIdentifier(ExternalIdentifierEntityType.Person, personId, authority, trimmed, validFrom);
    }

    [GeneratedRegex(AuthorityPattern, RegexOptions.CultureInvariant)]
    private static partial Regex AuthorityRegex();
}
