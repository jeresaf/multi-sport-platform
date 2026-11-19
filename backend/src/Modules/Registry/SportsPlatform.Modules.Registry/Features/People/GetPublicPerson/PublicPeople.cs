using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.GetPublicPerson;

/// <summary>
/// Loads a person of the request's public tenant with what decides its exposure (S06 D18): the status, the profile flag,
/// the age status on the tenant's date and the active consent scopes, read from the consents on every request. The public
/// read and its photo route share it, so they can't disagree.
/// </summary>
public sealed class PublicPeople(RegistryDbContext context, TenantToday tenantToday)
{
    /// <summary>The person and what may be shown, or null when there is no such person in the tenant (RLS).</summary>
    public async Task<(Person Person, PublicExposure Exposure)?> FindAsync(Guid personId, CancellationToken cancellationToken)
    {
        var person = await context.Set<Person>().AsNoTracking()
            .Include(p => p.Consents)
            .SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
        if (person is null)
        {
            return null;
        }

        var today = await tenantToday.GetAsync(cancellationToken);
        var exposure = PublicExposure.Of(
            person.Status,
            person.PublicProfileEnabled,
            Age.StatusOn(person.DateOfBirth, today),
            GuardianConsent.ActiveScopes(person.Consents));
        return (person, exposure);
    }
}
