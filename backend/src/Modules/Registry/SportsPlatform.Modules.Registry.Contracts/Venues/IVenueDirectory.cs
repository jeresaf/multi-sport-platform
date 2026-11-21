namespace SportsPlatform.Modules.Registry.Contracts.Venues;

/// <summary>A venue's stored states (docs/domain/state-machines.md, Registry; S07 D12).</summary>
public enum VenueStatus
{
    Active,
    TemporarilyUnavailable,
    Closed,
    Archived,
}

/// <summary>
/// What another module may show of a venue (S07 D18): its names, where it is, its time zone (the local time of its
/// contests), the sport variants it can host (codes) and its status.
/// </summary>
public sealed record VenueSummary(
    Guid Id,
    string Name,
    string? ShortName,
    string TimeZone,
    string City,
    string Country,
    IReadOnlyList<string> SupportedVariants,
    VenueStatus Status);

/// <summary>
/// The venues of the scope's tenant by ID, for modules that point at venues (editions, fixtures; S07 D18). Call it after
/// the scope's tenant is set. The caller checks its own action; this read checks none and is not audited.
/// </summary>
public interface IVenueDirectory
{
    /// <summary>
    /// The summaries of the venues among <paramref name="venueIds"/> that exist in the scope's tenant, whatever their
    /// status, in no particular order; unknown IDs and other tenants' venues are left out.
    /// </summary>
    Task<IReadOnlyList<VenueSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> venueIds, CancellationToken cancellationToken);
}
