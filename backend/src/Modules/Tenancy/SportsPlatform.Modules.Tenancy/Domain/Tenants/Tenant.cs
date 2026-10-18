using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Domain.Tenants;

/// <summary>
/// A customer of the platform (platform-managed, global). Its status follows the tenant state machine
/// (docs/domain/state-machines.md); every change, and the creation, adds a row to its status history.
/// </summary>
public sealed class Tenant : AggregateRoot
{
    private readonly List<TenantStatusChange> _statusHistory = [];

    private Tenant(Guid id, string slug, TenantProfile profile)
        : base(id)
    {
        Slug = slug;
        Status = TenantStatus.Provisioning;
        Apply(profile);
    }

    private Tenant()
    {
    }

    public string Slug { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public string TimeZone { get; private set; } = string.Empty;

    public string DefaultLocale { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }

    /// <summary>Whether the tenant wants a public site; it is shown only while the tenant is <c>ACTIVE</c>.</summary>
    public bool PublicEnabled { get; private set; }

    /// <summary>
    /// The rows added in this unit of work: the history is never loaded with the tenant. Read history with
    /// <c>AsNoTracking</c>, or EF fix-up adds the rows it reads to this list.
    /// </summary>
    public IReadOnlyList<TenantStatusChange> StatusHistory => _statusHistory;

    public TenantProfile Profile => new(LegalName, DisplayName, CountryCode, TimeZone, DefaultLocale, CurrencyCode, PublicEnabled);

    /// <summary>A new tenant in <c>PROVISIONING</c>; <paramref name="slug"/> must be valid and not reserved.</summary>
    public static Tenant Create(string slug, TenantProfile profile, ICurrentActor actor, DateTimeOffset now)
    {
        if (!TenantSlug.HasValidFormat(slug) || TenantSlug.IsReserved(slug))
        {
            throw new ArgumentException("The slug must be valid and not reserved.", nameof(slug));
        }

        ArgumentNullException.ThrowIfNull(actor);
        var tenant = new Tenant(NewId(), slug, profile);
        tenant._statusHistory.Add(new TenantStatusChange(tenant.Id, null, tenant.Status, null, actor, now));
        tenant.Raise(new TenantCreatedDomainEvent(tenant.Id, tenant.Slug, tenant.Status));
        return tenant;
    }

    /// <summary>Replaces the profile; returns whether anything changed.</summary>
    public bool UpdateProfile(TenantProfile profile)
    {
        if (profile == Profile)
        {
            return false;
        }

        Apply(profile);
        return true;
    }

    /// <summary><c>PROVISIONING</c> → <c>ACTIVE</c>.</summary>
    public Result Activate(ICurrentActor actor, DateTimeOffset now) =>
        Transition(TenantStatus.Active, [TenantStatus.Provisioning], reason: null, actor, now);

    /// <summary><c>ACTIVE</c> → <c>SUSPENDED</c>, with a reason.</summary>
    public Result Suspend(string reason, ICurrentActor actor, DateTimeOffset now) =>
        Transition(TenantStatus.Suspended, [TenantStatus.Active], RequireReason(reason), actor, now);

    /// <summary><c>SUSPENDED</c> → <c>ACTIVE</c>.</summary>
    public Result Restore(ICurrentActor actor, DateTimeOffset now) =>
        Transition(TenantStatus.Active, [TenantStatus.Suspended], reason: null, actor, now);

    /// <summary><c>ACTIVE</c> or <c>SUSPENDED</c> → <c>ARCHIVED</c>, with a reason.</summary>
    public Result Archive(string reason, ICurrentActor actor, DateTimeOffset now) =>
        Transition(TenantStatus.Archived, [TenantStatus.Active, TenantStatus.Suspended], RequireReason(reason), actor, now);

    /// <summary><c>ARCHIVED</c> → <c>SUSPENDED</c>, with a reason (platform only); restoring is a separate step.</summary>
    public Result Unarchive(string reason, ICurrentActor actor, DateTimeOffset now) =>
        Transition(TenantStatus.Suspended, [TenantStatus.Archived], RequireReason(reason), actor, now);

    private Result Transition(TenantStatus to, TenantStatus[] from, string? reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!from.Contains(Status))
        {
            return Result.Failure(TenancyErrors.InvalidTransition);
        }

        var previous = Status;
        Status = to;
        _statusHistory.Add(new TenantStatusChange(Id, previous, to, reason, actor, now));
        Raise(new TenantStatusChangedDomainEvent(Id, previous, to));
        return Result.Success();
    }

    private static string RequireReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var trimmed = reason.Trim();
        if (trimmed.Length > TenantStatusChange.ReasonMaxLength)
        {
            throw new ArgumentException($"A reason is at most {TenantStatusChange.ReasonMaxLength} characters.", nameof(reason));
        }

        return trimmed;
    }

    private void Apply(TenantProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.LegalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.CountryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.TimeZone);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.DefaultLocale);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.CurrencyCode);

        LegalName = profile.LegalName;
        DisplayName = profile.DisplayName;
        CountryCode = profile.CountryCode;
        TimeZone = profile.TimeZone;
        DefaultLocale = profile.DefaultLocale;
        CurrencyCode = profile.CurrencyCode;
        PublicEnabled = profile.PublicEnabled;
    }
}
