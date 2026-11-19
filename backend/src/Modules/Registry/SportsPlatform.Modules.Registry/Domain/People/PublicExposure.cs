using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// What the public person read and its photo route may show of a person (S06 D18, ADR-0015), decided on every request
/// from the stored state, the age status on the tenant's date and the active consent scopes, so a withdrawn scope hides
/// at once without touching the profile flag (D15).
/// </summary>
/// <param name="Visible">The person is <c>ACTIVE</c>, the profile is on and, unless an adult, <c>PUBLIC_PROFILE</c> is
/// given. Otherwise the public read is <c>PERSON_NOT_FOUND</c>.</param>
/// <param name="ShowsDetails">Nationality and athlete profiles: adults only.</param>
/// <param name="ShowsPhoto">The photo, when there is one: adults, and others with <c>PHOTO</c>.</param>
public sealed record PublicExposure(bool Visible, bool ShowsDetails, bool ShowsPhoto)
{
    public static readonly PublicExposure Hidden = new(false, false, false);

    public static PublicExposure Of(
        PersonStatus status, bool publicProfileEnabled, AgeStatus ageStatus, IReadOnlyCollection<ConsentScope> activeConsentScopes)
    {
        ArgumentNullException.ThrowIfNull(activeConsentScopes);
        if (status != PersonStatus.Active || !publicProfileEnabled)
        {
            return Hidden;
        }

        if (!Age.IsTreatedAsMinor(ageStatus))
        {
            return new(true, true, true);
        }

        return activeConsentScopes.Contains(ConsentScope.PublicProfile)
            ? new(true, false, activeConsentScopes.Contains(ConsentScope.Photo))
            : Hidden;
    }
}
