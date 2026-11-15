namespace SportsPlatform.Modules.Tenancy.Contracts.Sports;

/// <summary>
/// The sport variants the scope's tenant has enabled (S05 D12), for the modules that create things of a sport (S06–S09).
/// Read under RLS: call after the scope's tenant is set.
/// </summary>
public interface IEnabledSports
{
    /// <summary>The enabled variants, by variant code.</summary>
    Task<IReadOnlyList<EnabledSport>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="variant"/> is enabled now (codes match exactly).</summary>
    Task<bool> IsEnabledAsync(string variant, CancellationToken cancellationToken);
}

/// <summary>An enabled sport variant and when its current enablement began.</summary>
public sealed record EnabledSport(string Variant, DateTimeOffset EnabledAt);
