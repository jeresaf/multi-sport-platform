using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People.Profiles;

/// <summary>
/// One thing a staff member does in a sport variant (S06 D12): the variant's code, a <c>staff_roles</c> code of its
/// definition (checked by the use case) and an optional qualification (free text, e.g. "Level 2 coaching certificate").
/// </summary>
public sealed record StaffCapabilityDetails(string Variant, string Role, string? Qualification);

/// <summary>
/// A person working as staff (coach, physio…; <c>registry.staff_profiles</c>, tenant-owned, S06 D12): a child of the
/// person, so a change to it or its capabilities moves the person's version (S06 D11). A person holds at most one open
/// staff profile (<see cref="ProfileTransitions.IsOpen"/>), with 1–<see cref="MaxCapabilities"/> capabilities, one per
/// variant and role. Codes are the definition's (AR-11): generic code never reads them.
/// </summary>
public sealed class StaffProfile : Entity, ITenantOwned, IRoleProfile
{
    public const int MaxCapabilities = 10;

    private readonly List<StaffCapability> _capabilities = [];

    /// <summary>A staff profile of a person; commands add one through <see cref="Person.AddStaffProfile"/>.</summary>
    public StaffProfile(Guid personId, IReadOnlyList<StaffCapabilityDetails> capabilities)
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

    private StaffProfile()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid PersonId { get; private set; }

    public ProfileStatus Status { get; private set; }

    /// <summary>What the person does, one per variant and role.</summary>
    public IReadOnlyList<StaffCapability> Capabilities => _capabilities;

    /// <summary>
    /// Whether <paramref name="capabilities"/> holds each variant and role at most once.
    /// </summary>
    public static bool AreDistinct(IReadOnlyCollection<StaffCapabilityDetails> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return capabilities.Select(c => (c.Variant, c.Role)).Distinct().Count() == capabilities.Count;
    }

    /// <summary>Whether the profile already holds <paramref name="variant"/> and <paramref name="role"/>.</summary>
    public bool Holds(string variant, string role) =>
        _capabilities.Any(c => c.Variant == variant && c.Role == role);

    void IRoleProfile.MoveTo(ProfileStatus status) => Status = status;

    /// <summary>
    /// Replaces the capabilities as a whole: one already held (same variant and role) is kept and takes the new
    /// qualification, one left out is removed, a new one is added.
    /// </summary>
    internal void Replace(IReadOnlyList<StaffCapabilityDetails> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentOutOfRangeException.ThrowIfZero(capabilities.Count, nameof(capabilities));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capabilities.Count, MaxCapabilities, nameof(capabilities));
        if (!AreDistinct(capabilities))
        {
            throw new ArgumentException("A staff profile holds a variant and role once.", nameof(capabilities));
        }

        _capabilities.RemoveAll(held => !capabilities.Any(c => c.Variant == held.Variant && c.Role == held.Role));
        foreach (var details in capabilities)
        {
            if (_capabilities.SingleOrDefault(held => held.Variant == details.Variant && held.Role == details.Role) is { } held)
            {
                held.Qualify(details.Qualification);
            }
            else
            {
                _capabilities.Add(new StaffCapability(Id, details));
            }
        }
    }
}

/// <summary>
/// One of a staff profile's capabilities (<c>registry.staff_capabilities</c>, tenant-owned): a variant, a
/// <c>staff_roles</c> code of its definition and an optional qualification.
/// </summary>
public sealed class StaffCapability : Entity, ITenantOwned
{
    public const int VariantMaxLength = AthleteProfile.VariantMaxLength;
    public const int RoleMaxLength = 50;
    public const int QualificationMaxLength = 100;

    /// <summary>A capability of a staff profile; commands set them through <see cref="Person.UpdateStaffProfile"/>.</summary>
    public StaffCapability(Guid staffProfileId, StaffCapabilityDetails details)
        : base(NewId())
    {
        if (staffProfileId == Guid.Empty)
        {
            throw new ArgumentException("A capability belongs to a staff profile.", nameof(staffProfileId));
        }

        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Variant, nameof(details));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(details.Variant.Length, VariantMaxLength, nameof(details));
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Role, nameof(details));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(details.Role.Length, RoleMaxLength, nameof(details));
        StaffProfileId = staffProfileId;
        Variant = details.Variant;
        Role = details.Role;
        Qualify(details.Qualification);
    }

    private StaffCapability()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid StaffProfileId { get; private set; }

    /// <summary>The sport variant's code; never changes.</summary>
    public string Variant { get; private set; } = string.Empty;

    /// <summary>A <c>staff_roles</c> code of the variant's definition; never changes.</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Free text, stored trimmed with inner whitespace collapsed; null when none.</summary>
    public string? Qualification { get; private set; }

    internal void Qualify(string? qualification) =>
        Qualification = Person.Optional(qualification, QualificationMaxLength, nameof(qualification));
}
