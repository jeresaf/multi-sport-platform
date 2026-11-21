using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Infrastructure;

/// <summary>
/// Each Registry aggregate root's conflict code (S07 D2), in one place: a lost update answers the code of the root whose
/// version failed. Organisations and teams join with S07-T2 and T3.
/// </summary>
public static class RegistryConcurrencyConflicts
{
    /// <summary>The conflict of <paramref name="entity"/> when it is an aggregate root of the module; otherwise null.</summary>
    public static Error? For(object entity) => entity switch
    {
        Person => RegistryErrors.PersonConcurrencyConflict,
        Venue => RegistryErrors.VenueConcurrencyConflict,
        _ => null,
    };
}
