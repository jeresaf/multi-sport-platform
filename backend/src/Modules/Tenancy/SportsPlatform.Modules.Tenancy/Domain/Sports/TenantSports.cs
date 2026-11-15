using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Domain.Sports;

/// <summary>
/// A tenant's enabled sport variants (its open <see cref="TenantSport"/> rows, S05 D12, D13): at most one per variant.
/// The sport quota counts distinct sports, so two variants of one sport count once.
/// </summary>
public sealed class TenantSports
{
    private readonly Dictionary<string, TenantSport> _enabled;

    public TenantSports(IEnumerable<TenantSport> enabled)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        _enabled = enabled.Where(sport => sport.IsEnabled).ToDictionary(sport => sport.Variant, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<TenantSport> Enabled => _enabled.Values;

    public TenantSport? Find(string variant) => _enabled.GetValueOrDefault(variant);

    /// <summary>
    /// How many sports the quota counts: distinct sports of the enabled variants, read through
    /// <paramref name="sportOf"/> (a variant's sport; null for one no longer published, which still counts as its own).
    /// </summary>
    public int SportsCounted(Func<string, string?> sportOf)
    {
        ArgumentNullException.ThrowIfNull(sportOf);
        return _enabled.Keys.Select(variant => sportOf(variant) ?? variant).Distinct(StringComparer.Ordinal).Count();
    }

    /// <summary>Whether enabling a variant of <paramref name="sport"/> adds a sport to the count.</summary>
    public bool AddsSport(string sport, Func<string, string?> sportOf)
    {
        ArgumentNullException.ThrowIfNull(sportOf);
        return !_enabled.Keys.Any(variant => string.Equals(sportOf(variant), sport, StringComparison.Ordinal));
    }

    /// <summary>A new enablement; <c>TENANT_SPORT_ALREADY_ENABLED</c> when the variant is enabled.</summary>
    public Result<TenantSport> Enable(string variant, ICurrentActor actor, DateTimeOffset now)
    {
        if (_enabled.ContainsKey(variant))
        {
            return TenancyErrors.SportAlreadyEnabled;
        }

        var sport = TenantSport.Enable(variant, actor, now);
        _enabled.Add(variant, sport);
        return Result.Success(sport);
    }

    /// <summary>Closes the variant's enablement; <c>TENANT_SPORT_NOT_ENABLED</c> when it isn't enabled.</summary>
    public Result<TenantSport> Disable(string variant, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        if (!_enabled.Remove(variant, out var sport))
        {
            return TenancyErrors.SportNotEnabled;
        }

        sport.Disable(reason, actor, now);
        return Result.Success(sport);
    }
}
