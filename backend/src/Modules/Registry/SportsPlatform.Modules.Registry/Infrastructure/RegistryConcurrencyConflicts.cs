using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Infrastructure;

/// <summary>
/// Each Registry aggregate root's conflict code (S07 D2), in one place: a lost update answers the code of the root whose
/// version failed. Organisations and teams join with S07-T2 and T3; a test fails for a root mapped in
/// <see cref="RegistryDbContext"/> without a code here.
/// </summary>
public static class RegistryConcurrencyConflicts
{
    private static readonly Dictionary<Type, Error> _byRoot = new()
    {
        [typeof(Person)] = RegistryErrors.PersonConcurrencyConflict,
        [typeof(Venue)] = RegistryErrors.VenueConcurrencyConflict,
    };

    /// <summary>The conflict of an entity of <paramref name="type"/> when it is an aggregate root of the module; otherwise null.</summary>
    public static Error? For(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _byRoot.GetValueOrDefault(type);
    }
}
