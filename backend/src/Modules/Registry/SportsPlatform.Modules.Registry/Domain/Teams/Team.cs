using System.Globalization;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Domain.Teams;

/// <summary>
/// A team of a tenant in one sport variant (<c>registry.teams</c>, tenant-owned, S07 D6): its ID never changes (AR-23),
/// nor does its variant (another sport is another team). Its category (D7) is three generic codes. The name is the
/// playing name, kept as entered and as a normalised key (<see cref="NameNormaliser"/>) for the duplicate check (D13);
/// it changes only by rename (D8, S07-T3c), and <see cref="NameSince"/> is when the current name began; each earlier name
/// is a <see cref="AliasKind.FormerName"/> alias with its dates. The slug is unique in the tenant (D9). The status history
/// and the aliases are its children: a new or changed row changes the team's version (S06 D11).
/// Competition entries are not on the team (S09).
/// </summary>
public sealed class Team : AggregateRoot, ITenantOwned
{
    private readonly List<TeamStatusChange> _statusHistory = [];
    private readonly List<TeamAlias> _aliases = [];

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

    /// <summary>A <c>PUBLIC</c> media asset of Platform (S07 D15), set and removed by the logo actions.</summary>
    public Guid? LogoAssetId { get; private set; }

    /// <summary>A <c>PUBLIC</c> media asset of Platform (S07 D15), set and removed by the badge actions.</summary>
    public Guid? BadgeAssetId { get; private set; }

    public TeamStatus Status { get; private set; }

    public TeamSource Source { get; private set; }

    public IReadOnlyList<TeamStatusChange> StatusHistory => _statusHistory;

    /// <summary>Former names, nicknames and spellings, removed ones included (S07 D8, D10).</summary>
    public IReadOnlyList<TeamAlias> Aliases => _aliases;

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
    /// Replaces the editable fields (S07 D11) whatever the status: not the variant, which never changes, nor the name,
    /// which changes by rename (D8; <paramref name="details"/> carries the current name). The use case has checked the
    /// details, the slug and, when a compared field changes, the duplicate warning.
    /// </summary>
    public void Update(TeamDetails details, string slug)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (!Slugs.IsValid(slug))
        {
            throw new ArgumentException("The slug is not valid.", nameof(slug));
        }

        if (!string.Equals(details.Name, Name, StringComparison.Ordinal))
        {
            throw new ArgumentException("An update keeps the team's name: a new name is a rename.", nameof(details));
        }

        SetName(Name, details.ShortName);
        Apply(details);
        Slug = slug;
    }

    /// <summary>
    /// Whether <paramref name="details"/> change what the duplicate warning compares (S07 D13): the short name's key,
    /// the category or the organisation (the name and the variant don't change by update).
    /// </summary>
    public bool ChangesComparedFields(TeamDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return OptionalNameKeyOf(details.ShortName) != ShortNameKey
            || details.Category != Category
            || details.OrganisationId != OrganisationId;
    }

    /// <summary>
    /// The status the team had before it was archived (the source row of the latest move to <c>ARCHIVED</c>, Q55); null
    /// unless it is archived. Rows are ordered by time, then by ID.
    /// </summary>
    public TeamStatus? StatusBeforeArchive() => Status != TeamStatus.Archived
        ? null
        : _statusHistory
            .Where(change => change.ToStatus == TeamStatus.Archived)
            .OrderBy(change => change.OccurredAt).ThenBy(change => change.Id)
            .LastOrDefault()?.FromStatus;

    /// <summary>
    /// Moves the team to <paramref name="to"/> with a reason (S07 D12, <see cref="TeamTransitions"/>) and writes the row
    /// of its history; a move the current status doesn't allow is <c>TEAM_STATUS_INVALID</c>. Needs the history loaded
    /// (it throws otherwise): unarchiving reads the status before archiving from it. The quota (reform, unarchive) is
    /// the use case's.
    /// </summary>
    public Result ChangeStatus(TeamStatus to, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        // Every team has its creation row: an empty history means the caller didn't load it (a bug, not a refusal).
        if (_statusHistory.Count == 0)
        {
            throw new InvalidOperationException("The team's status history is not loaded.");
        }

        if (!TeamTransitions.IsAllowed(Status, to, StatusBeforeArchive()))
        {
            return Result.Failure(RegistryErrors.TeamStatusInvalid);
        }

        var from = Status;
        Status = to;
        _statusHistory.Add(new TeamStatusChange(Id, from, to, reason.Trim(), actor, now));
        return Result.Success();
    }

    /// <summary>
    /// Whether the team takes a new child (S07 D10): an alias, an identifier, a logo or badge, or a rename (its former name is a new
    /// alias; owner, S07-T3c plan). An archived team is <c>TEAM_ARCHIVED</c>, a disbanded one <c>TEAM_DISBANDED</c>;
    /// removing and ending stay allowed.
    /// </summary>
    public Result EnsureAcceptsNewChildren() => Status switch
    {
        TeamStatus.Archived => Result.Failure(RegistryErrors.TeamArchived),
        TeamStatus.Disbanded => Result.Failure(RegistryErrors.TeamDisbanded),
        _ => Result.Success(),
    };

    /// <summary>
    /// Names <paramref name="assetId"/> as the logo (S07 D15) and returns the asset it named before, if any; refused as a
    /// new child (<see cref="EnsureAcceptsNewChildren"/>).
    /// </summary>
    public Result<Guid?> SetLogo(Guid assetId)
    {
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        var previous = LogoAssetId;
        LogoAssetId = assetId;
        return Result.Success(previous);
    }

    /// <summary>Clears the logo in any status (S07 D10) and returns its asset: <c>TEAM_LOGO_NOT_FOUND</c> without one.</summary>
    public Result<Guid> RemoveLogo()
    {
        if (LogoAssetId is not { } assetId)
        {
            return RegistryErrors.TeamLogoNotFound;
        }

        LogoAssetId = null;
        return Result.Success(assetId);
    }

    /// <summary>
    /// Names <paramref name="assetId"/> as the badge (S07 D15) and returns the asset it named before, if any; refused as a
    /// new child (<see cref="EnsureAcceptsNewChildren"/>).
    /// </summary>
    public Result<Guid?> SetBadge(Guid assetId)
    {
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        var previous = BadgeAssetId;
        BadgeAssetId = assetId;
        return Result.Success(previous);
    }

    /// <summary>Clears the badge in any status (S07 D10) and returns its asset: <c>TEAM_BADGE_NOT_FOUND</c> without one.</summary>
    public Result<Guid> RemoveBadge()
    {
        if (BadgeAssetId is not { } assetId)
        {
            return RegistryErrors.TeamBadgeNotFound;
        }

        BadgeAssetId = null;
        return Result.Success(assetId);
    }

    /// <summary>
    /// Renames the team from <paramref name="effectiveOn"/> (S07 D8): the current name, with its short name and code,
    /// becomes a <see cref="AliasKind.FormerName"/> alias from <see cref="NameSince"/> to the day before
    /// <paramref name="effectiveOn"/>, and the new name starts then. <paramref name="shortName"/> and
    /// <paramref name="code"/> are the values from then on (the use case keeps the current ones when the request left
    /// them out). The slug doesn't change (D9). Refused on an archived or disbanded team (<see cref="EnsureAcceptsNewChildren"/>);
    /// <paramref name="effectiveOn"/> on or before the current name's start, or after <paramref name="today"/> (the
    /// tenant's date), is <c>TEAM_RENAME_DATE_INVALID</c>; the same name exactly is <c>TEAM_NAME_UNCHANGED</c>. Needs the
    /// aliases loaded. The use case has checked that the name has a letter or a digit and run the duplicate warning.
    /// </summary>
    public Result Rename(string name, string? shortName, string? code, DateOnly effectiveOn, DateOnly today)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts;
        }

        if (effectiveOn <= NameSince || effectiveOn > today)
        {
            return Result.Failure(RegistryErrors.TeamRenameDateInvalid);
        }

        if (string.Equals(name, Name, StringComparison.Ordinal))
        {
            return Result.Failure(RegistryErrors.TeamNameUnchanged);
        }

        _aliases.Add(TeamAlias.FormerName(Id, Name, ShortName, Code, NameSince, effectiveOn.AddDays(-1)));
        SetName(name, shortName);
        Code = code;
        NameSince = effectiveOn;
        return Result.Success();
    }

    /// <summary>
    /// The team's name, short name and code on <paramref name="date"/> (S07 D8, D18): on or after
    /// <see cref="NameSince"/>, the current ones; before it, the former name whose dates cover <paramref name="date"/>;
    /// before the first name's start, the first name (the earliest former name, or the current one if it was never
    /// renamed). Needs the former names loaded.
    /// </summary>
    public TeamNameOnDate NameOn(DateOnly date)
    {
        if (date >= NameSince)
        {
            return new(Name, ShortName, Code);
        }

        var former = _aliases.Where(alias => alias.IsFormerName).OrderBy(alias => alias.ValidFrom).ToList();
        var match = former.FirstOrDefault(alias => alias.Covers(date)) ?? former.FirstOrDefault();
        return match is null ? new(Name, ShortName, Code) : new(match.Name, match.ShortName, match.Code);
    }

    /// <summary>
    /// Adds a nickname or a spelling (S07 D8, D10; a former name comes only from a rename), so the directory finds the
    /// team under it too. Refused on an archived or disbanded team, and when the alias's key is already one of the
    /// team's (its name or short name, or a current alias, former names included: <c>TEAM_ALIAS_EXISTS</c>). Needs the
    /// aliases loaded. The use case has checked that the name has a letter or a digit.
    /// </summary>
    public Result<TeamAlias> AddAlias(string name, AliasKind kind)
    {
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        var alias = TeamAlias.Other(Id, name, kind);
        if (alias.NameKey == NameKey || alias.NameKey == ShortNameKey
            || _aliases.Any(existing => !existing.IsRemoved && existing.NameKey == alias.NameKey))
        {
            return RegistryErrors.TeamAliasExists;
        }

        _aliases.Add(alias);
        return Result.Success(alias);
    }

    /// <summary>
    /// Removes a nickname or a spelling, keeping it with who, when and why (S07 D10); allowed in every status. An alias of
    /// another team is <c>TEAM_ALIAS_NOT_FOUND</c>, a former name <c>TEAM_ALIAS_FORMER_NAME</c> (the name's history stays;
    /// owner, S07-T3c plan), one already removed <c>TEAM_ALIAS_REMOVED</c>.
    /// </summary>
    public Result RemoveAlias(Guid aliasId, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        var alias = _aliases.SingleOrDefault(existing => existing.Id == aliasId);
        if (alias is null)
        {
            return Result.Failure(RegistryErrors.TeamAliasNotFound);
        }

        if (alias.IsFormerName)
        {
            return Result.Failure(RegistryErrors.TeamAliasFormerName);
        }

        if (alias.IsRemoved)
        {
            return Result.Failure(RegistryErrors.TeamAliasRemoved);
        }

        alias.Remove(reason, actor, now);
        return Result.Success();
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
