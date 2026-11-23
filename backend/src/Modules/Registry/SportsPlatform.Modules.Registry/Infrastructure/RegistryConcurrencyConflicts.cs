using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Infrastructure;

/// <summary>
/// Each Registry aggregate root's conflict code (S07 D2), in one place: a lost update answers the code of the root whose
/// version failed. A test fails for a root mapped in
/// <see cref="RegistryDbContext"/> without a code here.
/// </summary>
public static class RegistryConcurrencyConflicts
{
    /// <summary>The conflict of an entity of <paramref name="type"/> when it is an aggregate root of the module; otherwise null.</summary>
    public static Error? For(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // A duplicate decision is about two people (S06 D7): its conflict is a person's.
        return type == typeof(Person) || type == typeof(DuplicateDecision) ? RegistryErrors.PersonConcurrencyConflict
            : type == typeof(Venue) ? RegistryErrors.VenueConcurrencyConflict
            : type == typeof(Organisation) ? RegistryErrors.OrganisationConcurrencyConflict
            : type == typeof(Team) ? RegistryErrors.TeamConcurrencyConflict
            : null;
    }
}
