using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.Tenancy.Contracts.Sports;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>
/// Checks a profile's sport codes as data (S06 D12; AR-11): a new profile's variant must be enabled for the tenant
/// (<see cref="IEnabledSports"/>, read under RLS), and each code must be one of the variant's latest definition
/// (<see cref="ISportDefinitions"/>). A disabled variant keeps its profiles (S05 D12), so a correction of one isn't
/// checked against the tenant's sports, and a staff capability already held may stay on a disabled variant. Never names
/// a sport.
/// </summary>
public sealed class SportCodes(IEnabledSports enabledSports, ISportDefinitions definitions)
{
    /// <summary>
    /// The first refusal of an athlete profile's codes: <c>PERSON_SPORT_NOT_ENABLED</c> on <c>variant</c> (only when
    /// <paramref name="isNew"/>), or <c>PERSON_POSITION_UNKNOWN</c> on <c>primaryPosition</c> or
    /// <c>otherPositions[i]</c>; null when every code is known.
    /// </summary>
    public async Task<FieldRefusal?> CheckAthleteAsync(
        string variant, bool isNew, AthleteProfileDetails details, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (isNew && !await enabledSports.IsEnabledAsync(variant, cancellationToken))
        {
            return new(RegistryErrors.SportNotEnabled, "variant");
        }

        // An enabled variant without a published definition can't be played; a profile's variant always had one.
        var definition = await definitions.GetAsync(variant, version: null, cancellationToken);
        if (definition is null)
        {
            return isNew
                ? new(RegistryErrors.SportNotEnabled, "variant")
                : throw new InvalidOperationException($"The profile's variant {variant} has no published definition.");
        }

        var positions = definition.Positions.Select(position => position.Code).ToHashSet(StringComparer.Ordinal);
        if (details.PrimaryPosition is { } primary && !positions.Contains(primary))
        {
            return new(RegistryErrors.PositionUnknown, "primaryPosition");
        }

        for (var i = 0; i < details.OtherPositions.Count; i++)
        {
            if (!positions.Contains(details.OtherPositions[i]))
            {
                return new(RegistryErrors.PositionUnknown, $"otherPositions[{i}]");
            }
        }

        return null;
    }

    /// <summary>
    /// The first refusal of a staff profile's capabilities, in order: <c>PERSON_SPORT_NOT_ENABLED</c> on
    /// <c>capabilities[i].variant</c> for a capability <paramref name="existing"/> doesn't already hold (all of them for a
    /// new profile: a disabled variant keeps the capabilities it has but takes no new one), or <c>PERSON_ROLE_UNKNOWN</c>
    /// on <c>capabilities[i].role</c> when the variant's latest definition has no such staff role; null when every code
    /// is known.
    /// </summary>
    public async Task<FieldRefusal?> CheckStaffAsync(
        IReadOnlyList<StaffCapabilityDetails> capabilities, StaffProfile? existing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var roles = new Dictionary<string, HashSet<string>?>(StringComparer.Ordinal);
        for (var i = 0; i < capabilities.Count; i++)
        {
            var (variant, role, _) = capabilities[i];
            var isNew = existing is null || !existing.Holds(variant, role);
            if (isNew && !await enabledSports.IsEnabledAsync(variant, cancellationToken))
            {
                return new(RegistryErrors.SportNotEnabled, $"capabilities[{i}].variant");
            }

            if (!roles.TryGetValue(variant, out var known))
            {
                var definition = await definitions.GetAsync(variant, version: null, cancellationToken);
                known = definition?.StaffRoles.Select(staffRole => staffRole.Code).ToHashSet(StringComparer.Ordinal);
                roles[variant] = known;
            }

            if (known is null)
            {
                // An enabled variant without a published definition can't be played; a held capability's always had one.
                return isNew
                    ? new(RegistryErrors.SportNotEnabled, $"capabilities[{i}].variant")
                    : throw new InvalidOperationException($"The capability's variant {variant} has no published definition.");
            }

            if (!known.Contains(role))
            {
                return new(RegistryErrors.RoleUnknown, $"capabilities[{i}].role");
            }
        }

        return null;
    }
}
