using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Domain.Organisations;

/// <summary>
/// A club, school, federation or other body of a tenant (<c>registry.organisations</c>, tenant-owned, S07 D4). Its ID
/// never changes (AR-23); its slug is unique in the tenant (D9). The display, legal and short names are kept as entered
/// and as normalised keys (<see cref="NameNormaliser"/>) for the duplicate check (D13), as is the city. The status
/// history, the parent links (D5) and the aliases (D10) are its children: a new or changed row changes the organisation's version (S06
/// D11). No named contact person: people belong to the person registry (D4).
/// </summary>
public sealed class Organisation : AggregateRoot, ITenantOwned
{
    private readonly List<OrganisationStatusChange> _statusHistory = [];
    private readonly List<OrganisationParentLink> _parentLinks = [];
    private readonly List<OrganisationAlias> _aliases = [];

    private Organisation()
    {
    }

    private Organisation(string slug, OrganisationSource source)
        : base(NewId())
    {
        Slug = slug;
        Source = source;
        Status = OrganisationStatus.Active;
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public OrganisationType Type { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The sorted normalised tokens of the display name (S06 D4): what the duplicate check compares.</summary>
    public string DisplayNameKey { get; private set; } = string.Empty;

    public string? LegalName { get; private set; }

    /// <summary>The legal name's key; null without a legal name or when it has no letter or digit.</summary>
    public string? LegalNameKey { get; private set; }

    public string? ShortName { get; private set; }

    /// <summary>The short name's key; null without a short name or when it has no letter or digit.</summary>
    public string? ShortNameKey { get; private set; }

    /// <summary>
    /// The normalised display name with its word order kept (<see cref="NameNormaliser.Normalise"/>): what the directory
    /// sorts and pages by (S07-T2b), so case, accents and punctuation don't move an organisation.
    /// </summary>
    public string SortKey { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Country { get; private set; } = string.Empty;

    public string? City { get; private set; }

    /// <summary>The normalised city: "the same city" in the duplicate check; null when there is none to compare.</summary>
    public string? CityKey { get; private set; }

    public int? FoundedYear { get; private set; }

    public string? Website { get; private set; }

    /// <summary>The body's own address (not a person's).</summary>
    public string? Email { get; private set; }

    /// <summary>The body's own number (not a person's).</summary>
    public string? Phone { get; private set; }

    public string? Description { get; private set; }

    /// <summary>A venue of the tenant (S07 D4); checked only when a request sets or changes it.</summary>
    public Guid? PrimaryVenueId { get; private set; }

    /// <summary>A <c>PUBLIC</c> media asset of Platform (S07 D15), set and removed by the logo actions.</summary>
    public Guid? LogoAssetId { get; private set; }

    public OrganisationStatus Status { get; private set; }

    public OrganisationSource Source { get; private set; }

    public IReadOnlyList<OrganisationStatusChange> StatusHistory => _statusHistory;

    /// <summary>The links to its parents (S07 D5), the current one open; loaded by the commands that change them.</summary>
    public IReadOnlyList<OrganisationParentLink> ParentLinks => _parentLinks;

    /// <summary>Its aliases (S07 D10), removed ones included; loaded by the commands that change them.</summary>
    public IReadOnlyList<OrganisationAlias> Aliases => _aliases;

    /// <summary>
    /// A new <c>ACTIVE</c> organisation in the scope's tenant (stamped on save), with the first row of its status
    /// history. The use case has checked <paramref name="details"/>, chosen a free <paramref name="slug"/> and run the
    /// duplicate check.
    /// </summary>
    public static Organisation Create(
        OrganisationDetails details, string slug, OrganisationSource source, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(actor);
        if (!Slugs.IsValid(slug))
        {
            throw new ArgumentException("The slug is not valid.", nameof(slug));
        }

        var organisation = new Organisation(slug, source);
        organisation.Apply(details);
        organisation._statusHistory.Add(new OrganisationStatusChange(organisation.Id, null, organisation.Status, null, actor, now));
        return organisation;
    }

    /// <summary>
    /// Whether <paramref name="details"/> change what the duplicate warning compares (S07 D13): a name key (display,
    /// legal or short), the type or the city key. A change of case, accents, punctuation or word order in a name changes
    /// none of them.
    /// </summary>
    public bool ChangesComparedFields(OrganisationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return NameKeyOf(details.DisplayName) != DisplayNameKey
            || OptionalNameKeyOf(details.LegalName) != LegalNameKey
            || OptionalNameKeyOf(details.ShortName) != ShortNameKey
            || details.Type != Type
            || CityKeyOf(details.City) != CityKey;
    }

    /// <summary>
    /// Replaces the editable fields and the slug (S07 D11, D9), whatever the status: a correction (owner, T2b plan, as
    /// venues'). The use case has checked <paramref name="details"/>, the slug's place in the tenant and, for a change to
    /// a compared field, the duplicate warning. The status, its history, the logo and the source don't change.
    /// </summary>
    public void Update(OrganisationDetails details, string slug)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (!Slugs.IsValid(slug))
        {
            throw new ArgumentException("The slug is not valid.", nameof(slug));
        }

        Apply(details);
        Slug = slug;
    }

    /// <summary>
    /// The status the organisation had before it was archived (the source row of the latest move to <c>ARCHIVED</c>,
    /// Q55); null unless it is archived. Rows are ordered by time, then by ID.
    /// </summary>
    public OrganisationStatus? StatusBeforeArchive() => Status != OrganisationStatus.Archived
        ? null
        : _statusHistory
            .Where(change => change.ToStatus == OrganisationStatus.Archived)
            .OrderBy(change => change.OccurredAt).ThenBy(change => change.Id)
            .LastOrDefault()?.FromStatus;

    /// <summary>
    /// Moves the organisation to <paramref name="to"/> with a reason (S07 D12, <see cref="OrganisationTransitions"/>) and
    /// writes the row of its history; a move the current status doesn't allow is <c>ORGANISATION_STATUS_INVALID</c>.
    /// Its teams keep their statuses. Needs the history loaded (it throws otherwise): unarchiving reads the status
    /// before archiving from it.
    /// </summary>
    public Result ChangeStatus(OrganisationStatus to, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        // Every organisation has its creation row: an empty history means the caller didn't load it (a bug, not a refusal).
        if (_statusHistory.Count == 0)
        {
            throw new InvalidOperationException("The organisation's status history is not loaded.");
        }

        if (!OrganisationTransitions.IsAllowed(Status, to, StatusBeforeArchive()))
        {
            return Result.Failure(RegistryErrors.OrganisationStatusInvalid);
        }

        var from = Status;
        Status = to;
        _statusHistory.Add(new OrganisationStatusChange(Id, from, to, reason.Trim(), actor, now));
        return Result.Success();
    }

    /// <summary>
    /// Whether the organisation takes a new child row, such as a parent link, an alias or an identifier (S07 D10): not when it is <c>ARCHIVED</c>
    /// (<c>ORGANISATION_ARCHIVED</c>) or <c>DISSOLVED</c> (<c>ORGANISATION_DISSOLVED</c>). Ending or clearing one stays
    /// allowed in every status (S06's pattern).
    /// </summary>
    public Result EnsureAcceptsNewChildren() => Status switch
    {
        OrganisationStatus.Archived => Result.Failure(RegistryErrors.OrganisationArchived),
        OrganisationStatus.Dissolved => Result.Failure(RegistryErrors.OrganisationDissolved),
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

    /// <summary>
    /// Clears the logo in any status (S07 D10) and returns the asset it named: <c>ORGANISATION_LOGO_NOT_FOUND</c> without one.
    /// </summary>
    public Result<Guid> RemoveLogo()
    {
        if (LogoAssetId is not { } assetId)
        {
            return RegistryErrors.OrganisationLogoNotFound;
        }

        LogoAssetId = null;
        return Result.Success(assetId);
    }

    /// <summary>Whether an organisation in <paramref name="status"/> may become a parent (S07 D5): not dissolved or archived.</summary>
    public static bool CanBeParent(OrganisationStatus status) =>
        status is not (OrganisationStatus.Dissolved or OrganisationStatus.Archived);

    /// <summary>The current parent (the open link's), or null. Needs the parent links loaded.</summary>
    public Guid? CurrentParentId => _parentLinks.SingleOrDefault(link => link.IsOpen)?.ParentId;

    /// <summary>
    /// Sets the parent (S07 D5): ends the open link on <paramref name="today"/> (the tenant's date) and, unless
    /// <paramref name="parent"/> is null (no parent), opens a link to it from that day. Needs the parent links loaded. The
    /// use case holds <c>registry.organisations:&lt;tenantId&gt;</c> and has checked that <paramref name="parent"/> is not a
    /// descendant; this refuses the rest: a dissolved or archived organisation takes no new parent (clearing stays
    /// allowed), the organisation itself is <c>ORGANISATION_PARENT_CYCLE</c>, a dissolved or archived parent
    /// <c>ORGANISATION_PARENT_UNAVAILABLE</c>. Setting the parent it already has (or none when it has none) changes
    /// nothing (owner, S07-T2c plan). Statuses don't follow the parent (RULE-ORG-005).
    /// </summary>
    public Result SetParent(
        Organisation? parent, string reason, ICurrentActor actor, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (parent?.Id == CurrentParentId)
        {
            return Result.Success();
        }

        if (parent is not null)
        {
            var accepts = EnsureAcceptsNewChildren();
            if (accepts.IsFailure)
            {
                return accepts;
            }

            if (parent.Id == Id)
            {
                return Result.Failure(RegistryErrors.OrganisationParentCycle);
            }

            if (!CanBeParent(parent.Status))
            {
                return Result.Failure(RegistryErrors.OrganisationParentUnavailable);
            }
        }

        _parentLinks.SingleOrDefault(link => link.IsOpen)?.End(today);
        if (parent is not null)
        {
            _parentLinks.Add(new OrganisationParentLink(Id, parent.Id, today, reason.Trim(), actor, now));
        }

        return Result.Success();
    }

    /// <summary>
    /// Adds an alias with its own name key, so the directory and the duplicate warning find the organisation under that
    /// name too (S07 D10, D13). Refused on a dissolved or archived organisation, and when the alias's key is already one
    /// of the organisation's (display, legal or short name, or a current alias: <c>ORGANISATION_ALIAS_EXISTS</c>). Needs
    /// the aliases loaded. The use case has checked that the name has a letter or a digit.
    /// </summary>
    public Result<OrganisationAlias> AddAlias(string name, AliasKind kind)
    {
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        var alias = new OrganisationAlias(Id, name, kind);
        if (alias.NameKey == DisplayNameKey || alias.NameKey == LegalNameKey || alias.NameKey == ShortNameKey
            || _aliases.Any(existing => !existing.IsRemoved && existing.NameKey == alias.NameKey))
        {
            return RegistryErrors.OrganisationAliasExists;
        }

        _aliases.Add(alias);
        return Result.Success(alias);
    }

    /// <summary>
    /// Removes an alias, keeping it with who, when and why (S07 D10); allowed in every status. An alias of another
    /// organisation is <c>ORGANISATION_ALIAS_NOT_FOUND</c>, one already removed <c>ORGANISATION_ALIAS_REMOVED</c>.
    /// </summary>
    public Result RemoveAlias(Guid aliasId, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        var alias = _aliases.SingleOrDefault(existing => existing.Id == aliasId);
        if (alias is null)
        {
            return Result.Failure(RegistryErrors.OrganisationAliasNotFound);
        }

        if (alias.IsRemoved)
        {
            return Result.Failure(RegistryErrors.OrganisationAliasRemoved);
        }

        alias.Remove(reason, actor, now);
        return Result.Success();
    }

    /// <summary>The name key of an organisation's name; empty when it has no letter or digit.</summary>
    public static string NameKeyOf(string? name) => NameNormaliser.NameKey(name);

    /// <summary>The key of an optional name: null without one or when it has no letter or digit.</summary>
    public static string? OptionalNameKeyOf(string? name) => NameKeyOf(name) is { Length: > 0 } key ? key : null;

    /// <summary>The directory's sort key of a display name; empty when it has no letter or digit.</summary>
    public static string SortKeyOf(string? name) => NameNormaliser.Normalise(name);

    /// <summary>The key "the same city" compares; null for no city or one without a letter or digit.</summary>
    public static string? CityKeyOf(string? city) => NameNormaliser.Normalise(city) is { Length: > 0 } key ? key : null;

    private void Apply(OrganisationDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Country);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(details.DisplayName.Length, OrganisationDetails.DisplayNameMaxLength, nameof(details));
        if (details.FoundedYear < OrganisationDetails.MinFoundedYear)
        {
            throw new ArgumentOutOfRangeException(nameof(details), "The founded year is out of bounds.");
        }

        var displayNameKey = NameKeyOf(details.DisplayName);
        if (displayNameKey.Length == 0)
        {
            throw new ArgumentException("An organisation's display name needs a letter or a digit.", nameof(details));
        }

        Type = details.Type;
        DisplayName = details.DisplayName;
        DisplayNameKey = displayNameKey;
        SortKey = SortKeyOf(details.DisplayName);
        LegalName = details.LegalName;
        LegalNameKey = OptionalNameKeyOf(details.LegalName);
        ShortName = details.ShortName;
        ShortNameKey = OptionalNameKeyOf(details.ShortName);
        Country = details.Country;
        City = details.City;
        CityKey = CityKeyOf(details.City);
        FoundedYear = details.FoundedYear;
        Website = details.Website;
        Email = details.Email;
        Phone = details.Phone;
        Description = details.Description;
        PrimaryVenueId = details.PrimaryVenueId;
    }
}
