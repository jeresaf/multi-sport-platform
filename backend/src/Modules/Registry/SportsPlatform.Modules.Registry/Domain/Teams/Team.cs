using System.Globalization;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Domain.Teams;

/// <summary>
/// A team of a tenant in one sport variant (<c>registry.teams</c>, tenant-owned, S07 D6): its ID never changes (AR-23),
/// nor does its variant (another sport is another team). Its category (D7) is three generic codes. The name is the
/// playing name, kept as entered and as a normalised key (<see cref="NameNormaliser"/>) for the duplicate check (D13);
/// it changes only by rename (D8, S07-T3c), and <see cref="NameSince"/> is when the current name began. The slug is
/// unique in the tenant (D9). The status history is its child: a new row changes the team's version (S06 D11).
/// Competition entries are not on the team (S09).
/// </summary>
public sealed class Team : AggregateRoot, ITenantOwned
{
    private readonly List<TeamStatusChange> _statusHistory = [];

    private Team()
    {
    }

    private Team(string variant, string slug, DateOnly nameSince, TeamSource source)
        : base(NewId())
    {
        Variant = variant;
        Slug = slug;
        NameSince = nameSince;
        Source = source;
        Status = TeamStatus.Active;
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>The sport variant's code (S05), enabled for the tenant when the team was created; it never changes.</summary>
    public string Variant { get; private set; } = string.Empty;

    public GenderCategory GenderCategory { get; private set; }

    public AgeGroup AgeGroup { get; private set; }

    public TeamLevel Level { get; private set; }

    /// <summary>The category as one value.</summary>
    public TeamCategory Category => new(GenderCategory, AgeGroup, Level);

    /// <summary>The organisation the team belongs to, if any (S07 D6); checked only when a request sets or changes it.</summary>
    public Guid? OrganisationId { get; private set; }

    /// <summary>The playing name, e.g. "Ntinda Swifts FC".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The sorted normalised tokens of the name (S06 D4): what the duplicate check compares.</summary>
    public string NameKey { get; private set; } = string.Empty;

    public string? ShortName { get; private set; }

    /// <summary>The short name's key; null without a short name or when it has no letter or digit.</summary>
    public string? ShortNameKey { get; private set; }

    /// <summary>The normalised name with its word order kept (<see cref="NameNormaliser.Normalise"/>): the directory's order.</summary>
    public string SortKey { get; private set; } = string.Empty;

    /// <summary>The day the current name began (S07 D8): a create's <c>nameSince</c>, then a rename's <c>effectiveOn</c>.</summary>
    public DateOnly NameSince { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    /// <summary>Three upper-case letters for scoreboards; not unique.</summary>
    public string? Code { get; private set; }

    public string? Motto { get; private set; }

    /// <summary><c>#RRGGBB</c>, upper case.</summary>
    public string? PrimaryColour { get; private set; }

    public string? SecondaryColour { get; private set; }

    public string? TertiaryColour { get; private set; }

    /// <summary>A venue of the tenant (S07 D6); checked only when a request sets or changes it.</summary>
    public Guid? HomeVenueId { get; private set; }

    public int? FoundedYear { get; private set; }

    /// <summary>A <c>PUBLIC</c> media asset of Platform (S07 D15); set by S07-T4's logo actions.</summary>
    public Guid? LogoAssetId { get; private set; }

    /// <summary>A <c>PUBLIC</c> media asset of Platform (S07 D15); set by S07-T4's badge actions.</summary>
    public Guid? BadgeAssetId { get; private set; }

    public TeamStatus Status { get; private set; }

    public TeamSource Source { get; private set; }

    public IReadOnlyList<TeamStatusChange> StatusHistory => _statusHistory;

    /// <summary>
    /// A new <c>ACTIVE</c> team in the scope's tenant (stamped on save), with the first row of its status history. The
    /// use case has checked that <paramref name="variant"/> is enabled, checked <paramref name="details"/> and
    /// <paramref name="nameSince"/>, chosen a free <paramref name="slug"/>, checked <c>MAX_TEAMS</c> and run the duplicate
    /// check.
    /// </summary>
    public static Team Create(
        string variant,
        TeamDetails details,
        DateOnly nameSince,
        string slug,
        TeamSource source,
        ICurrentActor actor,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variant);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(variant.Length, TeamDetails.VariantMaxLength, nameof(variant));
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(actor);
        if (!Slugs.IsValid(slug))
        {
            throw new ArgumentException("The slug is not valid.", nameof(slug));
        }

        if (nameSince < TeamDetails.MinNameSince)
        {
            throw new ArgumentOutOfRangeException(nameof(nameSince), "The name's start is out of bounds.");
        }

        var team = new Team(variant, slug, nameSince, source);
        team.SetName(details.Name, details.ShortName);
        team.Apply(details);
        team._statusHistory.Add(new TeamStatusChange(team.Id, null, team.Status, null, actor, now));
        return team;
    }

    /// <summary>
    /// The slug proposed for a team (S07 D9; owner, S07-T3a plan): the name, then the age group and gender codes, then the
    /// level unless it is <see cref="TeamLevel.First"/> (<c>ntinda-swifts-fc-senior-men</c>,
    /// <c>kireka-kingfishers-fc-senior-men-reserve</c>). Codes, not labels: "Boys" and "Girls" are the web app's words. A long
    /// name is cut, never the category (review #127 R2).
    /// </summary>
    public static string ProposeSlug(string name, TeamCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return Slugs.ProposeWithSuffix(
            "team",
            name,
            SlugPart(category.AgeGroup),
            SlugPart(category.Gender),
            category.Level == TeamLevel.First ? null : SlugPart(category.Level));
    }

    /// <summary>The name key of a team's name; empty when it has no letter or digit.</summary>
    public static string NameKeyOf(string? name) => NameNormaliser.NameKey(name);

    /// <summary>The key of an optional name: null without one or when it has no letter or digit.</summary>
    public static string? OptionalNameKeyOf(string? name) => NameKeyOf(name) is { Length: > 0 } key ? key : null;

    /// <summary>The directory's sort key of a name; empty when it has no letter or digit.</summary>
    public static string SortKeyOf(string? name) => NameNormaliser.Normalise(name);

    private static string SlugPart<TEnum>(TEnum value)
        where TEnum : struct, Enum => value.ToString().ToLower(CultureInfo.InvariantCulture);

    private void SetName(string name, string? shortName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, TeamDetails.NameMaxLength, nameof(name));
        if (shortName is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(shortName.Length, TeamDetails.ShortNameMaxLength, nameof(shortName));
        }

        var nameKey = NameKeyOf(name);
        if (nameKey.Length == 0)
        {
            throw new ArgumentException("A team's name needs a letter or a digit.", nameof(name));
        }

        Name = name;
        NameKey = nameKey;
        SortKey = SortKeyOf(name);
        ShortName = shortName;
        ShortNameKey = OptionalNameKeyOf(shortName);
    }

    private void Apply(TeamDetails details)
    {
        if (!TeamDetails.ColoursInOrder(details.PrimaryColour, details.SecondaryColour, details.TertiaryColour))
        {
            throw new ArgumentException("A secondary colour needs a primary, a tertiary a secondary.", nameof(details));
        }

        if (details.FoundedYear < TeamDetails.MinFoundedYear)
        {
            throw new ArgumentOutOfRangeException(nameof(details), "The founded year is out of bounds.");
        }

        GenderCategory = details.Category.Gender;
        AgeGroup = details.Category.AgeGroup;
        Level = details.Category.Level;
        OrganisationId = details.OrganisationId;
        Code = details.Code;
        Motto = details.Motto;
        PrimaryColour = details.PrimaryColour;
        SecondaryColour = details.SecondaryColour;
        TertiaryColour = details.TertiaryColour;
        HomeVenueId = details.HomeVenueId;
        FoundedYear = details.FoundedYear;
    }
}
