namespace SportsPlatform.Modules.Tenancy.Domain.Settings;

/// <summary>
/// Whether a guardian's consent is needed before a minor is registered for a competition (Q11). Consent before public
/// exposure is always required, whatever this says.
/// </summary>
public enum MinorRegistrationConsent
{
    Required,
    Recommended,
    Off,
}
