using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.Tenancy.Contracts.Sports;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>
/// Checks a profile's sport codes as data (S06 D12; AR-11): a new profile's variant must be enabled for the tenant
/// (<see cref="IEnabledSports"/>, read under RLS), and each code must be one of the variant's latest definition
/// (<see cref="ISportDefinitions"/>). A disabled variant keeps its profiles (S05 D12), so a correction of one isn't
/// checked against the tenant's sports, and a staff or official capability already held may stay on a disabled variant. Never names
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
    /// The first refusal of a staff profile's capabilities (see <see cref="CheckRolesAsync"/>), against each variant's
    /// <c>staff_roles</c>.
    /// </summary>
    public Task<FieldRefusal?> CheckStaffAsync(
        IReadOnlyList<StaffCapabilityDetails> capabilities, StaffProfile? existing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return CheckRolesAsync(
            [.. capabilities.Select(c => (c.Variant, c.Role))],
            (variant, role) => existing?.Holds(variant, role) ?? false,
            definition => definition.StaffRoles.Select(staffRole => staffRole.Code),
            cancellationToken);
    }

    /// <summary>
    /// The first refusal of an official profile's capabilities (see <see cref="CheckRolesAsync"/>), against each variant's
    /// <c>official_roles</c>.
    /// </summary>
    public Task<FieldRefusal?> CheckOfficialAsync(
        IReadOnlyList<OfficialCapabilityDetails> capabilities, OfficialProfile? existing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return CheckRolesAsync(
            [.. capabilities.Select(c => (c.Variant, c.Role))],
            (variant, role) => existing?.Holds(variant, role) ?? false,
            definition => definition.OfficialRoles.Select(officialRole => officialRole.Code),
            cancellationToken);
    }

    /// <summary>
    /// The first refusal of a profile's capabilities, in order: <c>PERSON_SPORT_NOT_ENABLED</c> on
    /// <c>capabilities[i].variant</c> for a capability the profile doesn't already hold (<paramref name="held"/>; all of
    /// them for a new profile: a disabled variant keeps the capabilities it has but takes no new one), or
    /// <c>PERSON_ROLE_UNKNOWN</c> on <c>capabilities[i].role</c> when the variant's latest definition has no such role
    /// (<paramref name="roles"/>); null when every code is known.
    /// </summary>
    private async Task<FieldRefusal?> CheckRolesAsync(
        IReadOnlyList<(string Variant, string Role)> capabilities,
        Func<string, string, bool> held,
        Func<SportDefinition, IEnumerable<string>> roles,
        CancellationToken cancellationToken)
    {
        var known = new Dictionary<string, HashSet<string>?>(StringComparer.Ordinal);
        for (var i = 0; i < capabilities.Count; i++)
        {
            var (variant, role) = capabilities[i];
            var isNew = !held(variant, role);
            if (isNew && !await enabledSports.IsEnabledAsync(variant, cancellationToken))
            {
                return new(RegistryErrors.SportNotEnabled, $"capabilities[{i}].variant");
            }

            if (!known.TryGetValue(variant, out var codes))
            {
                var definition = await definitions.GetAsync(variant, version: null, cancellationToken);
                codes = definition is null ? null : roles(definition).ToHashSet(StringComparer.Ordinal);
                known[variant] = codes;
            }

            if (codes is null)
            {
                // An enabled variant without a published definition can't be played; a held capability's always had one.
                return isNew
                    ? new(RegistryErrors.SportNotEnabled, $"capabilities[{i}].variant")
                    : throw new InvalidOperationException($"The capability's variant {variant} has no published definition.");
            }

            if (!codes.Contains(role))
            {
                return new(RegistryErrors.RoleUnknown, $"capabilities[{i}].role");
            }
        }

        return null;
    }
}
