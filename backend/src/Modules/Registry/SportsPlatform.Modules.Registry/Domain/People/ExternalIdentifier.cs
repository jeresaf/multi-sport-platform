using System.Text.RegularExpressions;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// An identifier another authority gave a registry entity (<c>registry.external_identifiers</c>, tenant-owned, S06 D10;
/// docs/architecture/data-conventions.md, Identifiers), e.g. <c>KMSA_REG_NO</c> or <c>SCHOOL_ADMISSION_NO</c>. A value is
/// unique per tenant, authority and entity type, ended ones included, so it is never reused. The entity is named by
/// type and ID without a foreign key, because one table serves every registry entity; a change to an entity's
/// identifiers must mark the entity changed (<c>ModuleDbContext.MarkChanged</c>, S06 D11). It never changes the
/// entity's own ID (AR-23).
/// </summary>
public sealed partial class ExternalIdentifier : Entity, ITenantOwned
{
    /// <summary>
    /// Also the table's check constraint, so it keeps <c>$</c>. In .NET <c>$</c> matches before a final line feed too:
    /// a caller that validates input with it must refuse control characters as well.
    /// </summary>
    public const string AuthorityPattern = "^[A-Z][A-Z0-9_]{1,39}$";
    public const int AuthorityMaxLength = 40;
    public const int ValueMaxLength = 100;

    /// <summary>
    /// Authorities that name national identity documents: <c>SENSITIVE</c> (ADR-0015) and not collected in S06 (D10).
    /// </summary>
    public static readonly IReadOnlySet<string> ReservedAuthorities =
        new HashSet<string>(StringComparer.Ordinal) { "NATIONAL_ID", "NIN", "PASSPORT" };

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
    public static ExternalIdentifier ForPerson(Guid personId, string authority, string value, DateOnly? validFrom = null) =>
        Create(ExternalIdentifierEntityType.Person, personId, authority, value, validFrom);

    /// <summary>An identifier of a venue (S07 D10); the use case has validated the authority and the value.</summary>
    public static ExternalIdentifier ForVenue(Guid venueId, string authority, string value, DateOnly? validFrom = null) =>
        Create(ExternalIdentifierEntityType.Venue, venueId, authority, value, validFrom);

    public bool IsEnded => ValidTo is not null;

    /// <summary>
    /// Ends the identifier on <paramref name="validTo"/> (S06 D10); the row stays, so the value is never reused. One
    /// already ended is <c>&lt;AREA&gt;_EXTERNAL_ID_ENDED</c>; a date before <see cref="ValidFrom"/> or after
    /// <paramref name="today"/> (the tenant's date) is <c>&lt;AREA&gt;_EXTERNAL_ID_VALIDITY_INVALID</c> (owner, S06-T3b
    /// plan), the area being the entity type's (S07 D10).
    /// </summary>
    public Result End(DateOnly validTo, DateOnly today)
    {
        if (IsEnded)
        {
            return Result.Failure(EntityType == ExternalIdentifierEntityType.Venue ? RegistryErrors.VenueExternalIdEnded : RegistryErrors.ExternalIdEnded);
        }

        if (validTo > today || validTo < ValidFrom)
        {
            return Result.Failure(EntityType == ExternalIdentifierEntityType.Venue
                ? RegistryErrors.VenueExternalIdValidityInvalid
                : RegistryErrors.ExternalIdValidityInvalid);
        }

        ValidTo = validTo;
        return Result.Success();
    }

    private static ExternalIdentifier Create(
        ExternalIdentifierEntityType entityType, Guid entityId, string authority, string value, DateOnly? validFrom)
    {
        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("An identifier belongs to an entity.", nameof(entityId));
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

        return new ExternalIdentifier(entityType, entityId, authority, trimmed, validFrom);
    }

    [GeneratedRegex(AuthorityPattern, RegexOptions.CultureInvariant)]
    private static partial Regex AuthorityRegex();
}
