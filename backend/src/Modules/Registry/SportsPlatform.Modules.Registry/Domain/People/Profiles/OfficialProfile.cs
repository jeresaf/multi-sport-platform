using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People.Profiles;

/// <summary>
/// One thing an official does in a sport variant (S06 D12): the variant's code, an <c>official_roles</c> code of its
/// definition (checked by the use case), and optionally a grade (free text, e.g. "Class 1"), a licence number and the
/// date the licence is valid until. Each of the three is optional on its own (owner, S06-T4b2 plan): a school referee
/// may have no licence.
/// </summary>
public sealed record OfficialCapabilityDetails(
    string Variant, string Role, string? Grade, string? LicenceNumber, DateOnly? LicenceValidUntil);

/// <summary>
/// A person working as an official (referee, judge…; <c>registry.official_profiles</c>, tenant-owned, S06 D12): a child
/// of the person, so a change to it or its capabilities moves the person's version (S06 D11). A person holds at most one
/// open official profile (<see cref="ProfileTransitions.IsOpen"/>), with 1–<see cref="MaxCapabilities"/> capabilities,
/// one per variant and role. Codes are the definition's (AR-11): generic code never reads them.
/// </summary>
public sealed class OfficialProfile : Entity, ITenantOwned, IRoleProfile
{
    public const int MaxCapabilities = 10;

    private readonly List<OfficialCapability> _capabilities = [];

    /// <summary>An official profile of a person; commands add one through <see cref="Person.AddOfficialProfile"/>.</summary>
    public OfficialProfile(Guid personId, IReadOnlyList<OfficialCapabilityDetails> capabilities)
        : base(NewId())
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException("A profile belongs to a person.", nameof(personId));
        }

        PersonId = personId;
        Status = ProfileStatus.Active;
        Replace(capabilities);
    }

    private OfficialProfile()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid PersonId { get; private set; }

    public ProfileStatus Status { get; private set; }

    /// <summary>What the person officiates as, one per variant and role.</summary>
    public IReadOnlyList<OfficialCapability> Capabilities => _capabilities;

    /// <summary>Whether <paramref name="capabilities"/> holds each variant and role at most once.</summary>
    public static bool AreDistinct(IReadOnlyCollection<OfficialCapabilityDetails> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return capabilities.Select(c => (c.Variant, c.Role)).Distinct().Count() == capabilities.Count;
    }

    /// <summary>Whether the profile already holds <paramref name="variant"/> and <paramref name="role"/>.</summary>
    public bool Holds(string variant, string role) =>
        _capabilities.Any(c => c.Variant == variant && c.Role == role);

    void IRoleProfile.MoveTo(ProfileStatus status) => Status = status;

    /// <summary>
    /// Replaces the capabilities as a whole: one already held (same variant and role) is kept and takes the new grade and
    /// licence, one left out is removed, a new one is added.
    /// </summary>
    internal void Replace(IReadOnlyList<OfficialCapabilityDetails> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentOutOfRangeException.ThrowIfZero(capabilities.Count, nameof(capabilities));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capabilities.Count, MaxCapabilities, nameof(capabilities));
        if (!AreDistinct(capabilities))
        {
            throw new ArgumentException("An official profile holds a variant and role once.", nameof(capabilities));
        }

        _capabilities.RemoveAll(held => !capabilities.Any(c => c.Variant == held.Variant && c.Role == held.Role));
        foreach (var details in capabilities)
        {
            if (_capabilities.SingleOrDefault(held => held.Variant == details.Variant && held.Role == details.Role) is { } held)
            {
                held.Certify(details);
            }
            else
            {
                _capabilities.Add(new OfficialCapability(Id, details));
            }
        }
    }
}

/// <summary>
/// One of an official profile's capabilities (<c>registry.official_capabilities</c>, tenant-owned): a variant, an
/// <c>official_roles</c> code of its definition, and an optional grade, licence number and licence end date.
/// </summary>
public sealed class OfficialCapability : Entity, ITenantOwned
{
    public const int VariantMaxLength = AthleteProfile.VariantMaxLength;
    public const int RoleMaxLength = StaffCapability.RoleMaxLength;
    public const int GradeMaxLength = 50;
    public const int LicenceNumberMaxLength = 50;

    /// <summary>A capability of an official profile; commands set them through <see cref="Person.UpdateOfficialProfile"/>.</summary>
    public OfficialCapability(Guid officialProfileId, OfficialCapabilityDetails details)
        : base(NewId())
    {
        if (officialProfileId == Guid.Empty)
        {
            throw new ArgumentException("A capability belongs to an official profile.", nameof(officialProfileId));
        }

        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Variant, nameof(details));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(details.Variant.Length, VariantMaxLength, nameof(details));
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Role, nameof(details));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(details.Role.Length, RoleMaxLength, nameof(details));
        OfficialProfileId = officialProfileId;
        Variant = details.Variant;
        Role = details.Role;
        Certify(details);
    }

    private OfficialCapability()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid OfficialProfileId { get; private set; }

    /// <summary>The sport variant's code; never changes.</summary>
    public string Variant { get; private set; } = string.Empty;

    /// <summary>An <c>official_roles</c> code of the variant's definition; never changes.</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Free text, stored trimmed with inner whitespace collapsed; null when none.</summary>
    public string? Grade { get; private set; }

    /// <summary>The licence's number as the licensing body wrote it, stored trimmed with inner whitespace collapsed; null when none.</summary>
    public string? LicenceNumber { get; private set; }

    /// <summary>The last day the licence is valid; null when none was recorded.</summary>
    public DateOnly? LicenceValidUntil { get; private set; }

    /// <summary>
    /// Whether the licence has expired on <paramref name="today"/> (the tenant's date): it is valid through its last day,
    /// and one without an end date never expires (owner, S06-T4b2 plan). Nothing blocks on it in S06.
    /// </summary>
    public bool LicenceExpiredOn(DateOnly today) => LicenceValidUntil is { } until && until < today;

    /// <summary>Sets the grade and licence (the variant and role never change).</summary>
    internal void Certify(OfficialCapabilityDetails details)
    {
        Grade = Person.Optional(details.Grade, GradeMaxLength, nameof(details.Grade));
        LicenceNumber = Person.Optional(details.LicenceNumber, LicenceNumberMaxLength, nameof(details.LicenceNumber));
        LicenceValidUntil = details.LicenceValidUntil;
    }
}
