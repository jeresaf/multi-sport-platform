using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People.Profiles;

/// <summary>
/// What an athlete profile says beyond its variant (S06 D12): the primary position and up to
/// <see cref="AthleteProfile.MaxOtherPositions"/> others (codes of the variant's definition, checked by the use case),
/// the dominant side, and the public name (null: the person's display name).
/// </summary>
public sealed record AthleteProfileDetails(
    string? PrimaryPosition, IReadOnlyList<string> OtherPositions, DominantSide? DominantSide, string? PublicName);

/// <summary>
/// A person playing a sport variant (<c>registry.athlete_profiles</c>, tenant-owned, S06 D12): a child of the person,
/// so a change to it moves the person's version (S06 D11). Codes are the definition's (AR-11): generic code never reads
/// them. A person holds at most one open profile per variant (<see cref="ProfileTransitions.IsOpen"/>).
/// </summary>
public sealed class AthleteProfile : Entity, ITenantOwned
{
    public const int VariantMaxLength = 50;
    public const int PositionMaxLength = 50;
    public const int MaxOtherPositions = 5;
    public const int PublicNameMaxLength = PersonBiodata.DisplayNameMaxLength;

    private List<string> _otherPositions = [];

    /// <summary>An athlete profile of a person; commands add one through <see cref="Person.AddAthleteProfile"/>.</summary>
    public AthleteProfile(Guid personId, string variant, AthleteProfileDetails details)
        : base(NewId())
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException("A profile belongs to a person.", nameof(personId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(variant);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(variant.Length, VariantMaxLength, nameof(variant));
        PersonId = personId;
        Variant = variant;
        Status = ProfileStatus.Active;
        Apply(details);
    }

    private AthleteProfile()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid PersonId { get; private set; }

    /// <summary>The sport variant's code; never changes.</summary>
    public string Variant { get; private set; } = string.Empty;

    public string? PrimaryPosition { get; private set; }

    public IReadOnlyList<string> OtherPositions => _otherPositions;

    public DominantSide? DominantSide { get; private set; }

    /// <summary>The name shown with the sport (e.g. "J. Ssekandi"); null shows the person's display name.</summary>
    public string? PublicName { get; private set; }

    public ProfileStatus Status { get; private set; }

    internal void Update(AthleteProfileDetails details) => Apply(details);

    internal void MoveTo(ProfileStatus status) => Status = status;

    private void Apply(AthleteProfileDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var others = details.OtherPositions ?? [];
        ArgumentOutOfRangeException.ThrowIfGreaterThan(others.Count, MaxOtherPositions, nameof(details));
        if (others.Distinct(StringComparer.Ordinal).Count() != others.Count
            || (details.PrimaryPosition is not null && others.Contains(details.PrimaryPosition, StringComparer.Ordinal)))
        {
            throw new ArgumentException("Other positions are distinct and never the primary one.", nameof(details));
        }

        foreach (var code in others.Append(details.PrimaryPosition).OfType<string>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(details));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(code.Length, PositionMaxLength, nameof(details));
        }

        PrimaryPosition = details.PrimaryPosition;
        _otherPositions = [.. others];
        DominantSide = details.DominantSide;
        PublicName = Person.Optional(details.PublicName, PublicNameMaxLength, nameof(details.PublicName));
    }
}
