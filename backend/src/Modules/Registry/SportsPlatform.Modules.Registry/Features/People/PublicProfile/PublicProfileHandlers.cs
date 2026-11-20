using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.PublicProfile;

/// <summary>
/// Turns a person's public profile on (S06 D15): a person who isn't an adult on the tenant's date needs an active consent
/// with <c>PUBLIC_PROFILE</c> (<c>PERSON_CONSENT_REQUIRED</c>), so the consents are loaded before their active scopes are
/// read (review #106 N1). Refused on an archived or deceased person (D9). A profile already on is answered unchanged.
/// </summary>
public sealed class EnablePublicProfileHandler(RegistryDbContext context, TenantToday tenantToday, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(Guid personId, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        audit.Describe(PeopleEndpoints.EntityType, personId);

        var (person, refusal) = await PersonChanges.LoadAsync(
            context, personId, version: null, ifMatch, cancellationToken, withConsents: true);
        if (person is null)
        {
            return refusal;
        }

        var today = await tenantToday.GetAsync(cancellationToken);
        var enabled = person.EnablePublicProfile(
            Age.StatusOn(person.DateOfBirth, today), GuardianConsent.ActiveScopes(person.Consents));
        if (enabled.IsFailure)
        {
            return new(enabled.Error);
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>Turns a person's public profile off, in any status (S06 D15). A profile already off is answered unchanged.</summary>
public sealed class DisablePublicProfileHandler(RegistryDbContext context, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(Guid personId, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        audit.Describe(PeopleEndpoints.EntityType, personId);

        var (person, refusal) = await PersonChanges.LoadAsync(context, personId, version: null, ifMatch, cancellationToken);
        if (person is null)
        {
            return refusal;
        }

        person.DisablePublicProfile();
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}
