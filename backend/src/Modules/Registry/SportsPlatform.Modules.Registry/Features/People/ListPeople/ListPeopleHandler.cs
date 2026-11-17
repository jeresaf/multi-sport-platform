using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Registry.Infrastructure;
using PersonStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;

namespace SportsPlatform.Modules.Registry.Features.People.ListPeople;

public sealed record ListPeopleResponse(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<PersonListItem> Items,
    [property: PersonalData(PersonalDataClass.Internal)] PageInfo Page);

/// <summary>
/// The directory of the request's tenant (query filter and RLS), sorted by normalised last name, normalised first name
/// and ID (S06 D22). <c>q</c> matches a name or an alias (not a removed one) by prefix or trigram similarity, and an
/// external identifier's value exactly. Without a status, archived people are left out. <c>profile</c> and
/// <c>variant</c> keep people holding an open (not archived) profile of that kind and variant. The date of birth is read to
/// compute the birth year and the age status on the tenant's date, and goes no further.
/// </summary>
public sealed class ListPeopleHandler(RegistryDbContext context, TenantToday tenantToday)
{
    /// <summary>The least trigram similarity of a name key to the search text (pg_trgm's own default).</summary>
    public const double SearchSimilarity = 0.3;

    public async Task<ListPeopleResponse> HandleAsync(PeopleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var today = await tenantToday.GetAsync(cancellationToken);

        var people = context.Set<Person>().AsNoTracking();
        people = query.Status is { } status
            ? people.Where(p => p.Status == status)
            : people.Where(p => p.Status != PersonStatus.Archived);

        var latestAdult = Age.LatestAdultBirthDate(today);
        people = query.AgeStatus switch
        {
            AgeStatus.Adult => people.Where(p => p.DateOfBirth <= latestAdult),
            AgeStatus.Minor => people.Where(p => p.DateOfBirth > latestAdult),
            AgeStatus.Unknown => people.Where(p => p.DateOfBirth == null),
            _ => people,
        };

        // Only athlete profiles exist yet (S06-T4a): either filter reads them; T4b adds staff and official profiles.
        if (query.Profile is not null || query.Variant is not null)
        {
            var variant = query.Variant;
            people = people.Where(p => context.Set<AthleteProfile>().Any(a =>
                a.PersonId == p.Id && a.Status != ProfileStatus.Archived && (variant == null || a.Variant == variant)));
        }

        if (query.Search is { } search)
        {
            people = Matching(people, search);
        }

        if (query.After is { } after)
        {
            people = people.Where(p =>
                p.LastNameKey.CompareTo(after.LastNameKey) > 0
                || (p.LastNameKey == after.LastNameKey
                    && (p.FirstNameKey.CompareTo(after.FirstNameKey) > 0
                        || (p.FirstNameKey == after.FirstNameKey && p.Id.CompareTo(after.Id) > 0))));
        }

        var rows = await people
            .OrderBy(p => p.LastNameKey).ThenBy(p => p.FirstNameKey).ThenBy(p => p.Id)
            .Take(query.Limit + 1)
            .Select(p => new Row(
                p.Id, p.DisplayName, p.FirstName, p.LastName, p.LastNameKey, p.FirstNameKey, p.Status, p.DateOfBirth, p.Nationality))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = rows.Take(query.Limit).ToList();
        var items = page.Select(row =>
        {
            var ageStatus = Age.StatusOn(row.DateOfBirth, today);
            return new PersonListItem(
                row.Id, row.DisplayName, row.FirstName, row.LastName, row.Status, row.DateOfBirth?.Year,
                Age.IsTreatedAsMinor(ageStatus), ageStatus, row.Nationality);
        }).ToList();
        var next = hasMore ? new PersonCursor(page[^1].LastNameKey, page[^1].FirstNameKey, page[^1].Id).Encode() : null;
        return new ListPeopleResponse(items, new PageInfo(next, hasMore));
    }

    private IQueryable<Person> Matching(IQueryable<Person> people, string search)
    {
        // One part as typed ("ssek") for prefixes; the sorted tokens ("john ssekandi" for "Ssekandi John") to compare
        // with name keys. Text without a letter or a digit has no key and can only be an identifier's value.
        var prefix = NameNormaliser.Normalise(search);
        var key = NameNormaliser.NameKey(search);
        var identifiers = context.Set<ExternalIdentifier>()
            .Where(i => i.EntityType == ExternalIdentifierEntityType.Person && i.Value == search);
        if (prefix.Length == 0)
        {
            return people.Where(p => identifiers.Any(i => i.EntityId == p.Id));
        }

        // Keys hold letters, digits and spaces only, so neither pattern has a wildcard of its own.
        var prefixPattern = prefix + "%";
        var keyPattern = key + "%";
        return people.Where(p =>
            EF.Functions.Like(p.LastNameKey, prefixPattern)
            || EF.Functions.Like(p.FirstNameKey, prefixPattern)
            || EF.Functions.Like(p.NameKey, keyPattern)
            || EF.Functions.TrigramsSimilarity(p.NameKey, key) >= SearchSimilarity
            || p.Aliases.Any(a => a.RemovedAt == null
                && (EF.Functions.Like(a.NameKey, prefixPattern) || EF.Functions.TrigramsSimilarity(a.NameKey, key) >= SearchSimilarity))
            || identifiers.Any(i => i.EntityId == p.Id));
    }

    private sealed record Row(
        Guid Id, string DisplayName, string FirstName, string LastName, string LastNameKey, string FirstNameKey,
        PersonStatus Status, DateOnly? DateOfBirth, string? Nationality);
}
