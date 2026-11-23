using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The organisation duplicate warning (S07 D13, T2 acceptance): only organisations of the same type or in the same city
/// are compared, a missing city never matches; <c>LIKELY</c> when a name key (display, legal or short, or a current
/// alias; owner, S07-T2d plan: both sides) of one is one of the other's; <c>POSSIBLE</c> when the display names are at least 0.7 similar (owner, plan 2026-11-22); at most ten,
/// strongest first.
/// </summary>
public sealed class OrganisationDuplicateCheckTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    // The existing organisation every case is checked against: a club in Kampala with a legal and a short name, a
    // current alias and a removed one.
    private const string _existing = "Bunga Breakers";

    // pg_trgm compares words' trigrams: "Bunga Breakers" and "Bunga Breakers Youth" share 14 of 20 (0.70),
    // "Bunga Breakers United" 14 of 21 (0.667).
    [Theory]
    [InlineData("same display name, same type, another city", "CLUB", "breakers,  BUNGA", null, null, "Mukono", "LIKELY", "SAME_NAME_KEY SAME_TYPE")]
    [InlineData("same display name, another type, same city", "SCHOOL", "Bunga Breakers", null, null, " kampala ", "LIKELY", "SAME_NAME_KEY SAME_CITY")]
    [InlineData("same display name, same type and city", "CLUB", "Bunga Breakers", null, null, "Kampala", "LIKELY", "SAME_NAME_KEY SAME_TYPE SAME_CITY")]
    [InlineData("same display name, another type and city", "SCHOOL", "Bunga Breakers", null, null, "Mukono", null, null)]
    [InlineData("same display name, another type, no city", "SCHOOL", "Bunga Breakers", null, null, null, null, null)]
    [InlineData("the display name is its short name", "CLUB", "Breakers", null, null, "Mukono", "LIKELY", "SAME_NAME_KEY SAME_TYPE")]
    [InlineData("the legal name is its display name", "CLUB", "Lakeside Breakers", "Bunga Breakers", null, "Mukono", "LIKELY", "SAME_NAME_KEY SAME_TYPE")]
    [InlineData("the short name is its legal name", "SCHOOL", "Lakeside Academy", null, "Bunga Breakers Sports Club Limited", "Kampala", "LIKELY", "SAME_NAME_KEY SAME_CITY")]
    [InlineData("display names 0.70 similar, same type", "CLUB", "Bunga Breakers Youth", null, null, "Mukono", "POSSIBLE", "NAME_SIMILARITY:0.70 SAME_TYPE")]
    [InlineData("display names 0.70 similar, same city", "ACADEMY", "Bunga Breakers Youth", null, null, "Kampala", "POSSIBLE", "NAME_SIMILARITY:0.70 SAME_CITY")]
    [InlineData("display names 0.667 similar, same type and city", "CLUB", "Bunga Breakers United", null, null, "Kampala", null, null)]
    [InlineData("display names 0.70 similar, another type and city", "ACADEMY", "Bunga Breakers Youth", null, null, "Mukono", null, null)]
    [InlineData("a legal name 0.70 similar is no evidence", "CLUB", "Lakeside Juniors", "Bunga Breakers Youth", null, "Mukono", null, null)]
    [InlineData("the display name is its current alias", "CLUB", "Blazers, Lakeside", null, null, "Mukono", "LIKELY", "SAME_NAME_KEY SAME_TYPE")]
    [InlineData("the short name is its current alias", "SCHOOL", "Kololo Juniors", null, "Lakeside Blazers", "Kampala", "LIKELY", "SAME_NAME_KEY SAME_CITY")]
    [InlineData("its current alias, another type and city", "SCHOOL", "Lakeside Blazers", null, null, "Mukono", null, null)]
    [InlineData("its removed alias is no evidence", "CLUB", "Bunga Blue Army", null, null, "Mukono", null, null)]
    public async Task The_warning_compares_names_within_the_same_type_or_city(
        string because, string type, string displayName, string? legalName, string? shortName, string? city, string? confidence, string? evidence)
    {
        var (kmsa, registrar) = await TenantAsync();
        var existing = await SaveOrganisationAsync(
            App.Services, kmsa, _existing, legalName: "Bunga Breakers Sports Club Limited", shortName: "Breakers",
            alias: ("Lakeside Blazers", AliasKind.Nickname));
        await AddRemovedAliasAsync(kmsa, existing.Id, "Bunga Blue Army");

        var answer = await CheckOrganisationAsync(registrar, new { type, displayName, legalName, shortName, city });

        if (confidence is null)
        {
            OrganisationCandidates(answer).ShouldBeEmpty(because);
            return;
        }

        OrganisationCandidates(answer).ShouldBe([(existing.Id, confidence)], because);
        EvidenceOf(answer.GetProperty("candidates")[0]).ShouldBe(evidence, because);
    }

    [Fact]
    public async Task An_organisation_without_a_city_matches_only_by_type()
    {
        var (kmsa, registrar) = await TenantAsync();
        var noCity = await SaveOrganisationAsync(App.Services, kmsa, _existing, type: OrganisationType.Community, city: null);

        (await CheckOrganisationAsync(registrar, new { type = "SCHOOL", displayName = _existing, city = "Kampala" }))
            .GetProperty("candidates").GetArrayLength().ShouldBe(0);
        OrganisationCandidates(await CheckOrganisationAsync(registrar, new { type = "COMMUNITY", displayName = _existing }))
            .ShouldBe([(noCity.Id, "LIKELY")]);
    }

    [Fact]
    public async Task Candidates_come_strongest_first_at_most_ten_and_the_answer_has_what_the_form_shows()
    {
        var (kmsa, registrar) = await TenantAsync();
        for (var i = 0; i < 11; i++)
        {
            await SaveOrganisationAsync(App.Services, kmsa, "Bunga Breakers Youth");
        }

        var same = await SaveOrganisationAsync(App.Services, kmsa, _existing, city: "Wakiso");

        var answer = await CheckOrganisationAsync(registrar, new { type = "CLUB", displayName = _existing, city = "Kampala" });

        var candidates = answer.GetProperty("candidates").EnumerateArray().ToList();
        candidates.Count.ShouldBe(10);
        candidates[0].EnumerateObject().Select(property => property.Name)
            .ShouldBe(["organisationId", "displayName", "type", "city", "status", "confidence", "evidence"]);
        (candidates[0].GetProperty("organisationId").GetGuid(), candidates[0].GetProperty("displayName").GetString(),
                candidates[0].GetProperty("type").GetString(), candidates[0].GetProperty("city").GetString(),
                candidates[0].GetProperty("status").GetString(), candidates[0].GetProperty("confidence").GetString())
            .ShouldBe((same.Id, _existing, "CLUB", "Wakiso", "ACTIVE", "LIKELY"));
        candidates.Skip(1).ShouldAllBe(candidate => candidate.GetProperty("confidence").GetString() == "POSSIBLE");
    }

    // D13: archived organisations are found and marked. Archiving is S07-T2b's: the test sets the status directly.
    [Fact]
    public async Task An_archived_organisation_is_found_and_marked()
    {
        var (kmsa, registrar) = await TenantAsync();
        var archived = await SaveOrganisationAsync(App.Services, kmsa, _existing);
        await using (var scope = RegistryTestData.ScopeFor(App.Services, kmsa, out var context))
        {
            var organisation = await context.Set<Organisation>().SingleAsync(o => o.Id == archived.Id, Ct);
            context.Entry(organisation).Property(o => o.Status).CurrentValue = OrganisationStatus.Archived;
            await context.SaveChangesAsync(Ct);
        }

        var candidate = (await CheckOrganisationAsync(registrar, new { type = "CLUB", displayName = _existing }))
            .GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();

        (candidate.GetProperty("organisationId").GetGuid(), candidate.GetProperty("status").GetString(), candidate.GetProperty("confidence").GetString())
            .ShouldBe((archived.Id, "ARCHIVED", "LIKELY"));
    }

    // D13 "either side": the organisation being changed is known by its current aliases too, so they meet the other's
    // names and aliases. Adding an alias runs no warning (owner, S07-T2d plan): an update to a compared field does.
    [Theory]
    [InlineData("its alias is the other's display name", "Breakers, Bunga")]
    [InlineData("its alias is the other's alias", "Blazers of the Lakeside")]
    public async Task An_update_compares_the_organisations_own_aliases(string because, string alias)
    {
        var (kmsa, registrar) = await TenantAsync();
        var existing = await SaveOrganisationAsync(App.Services, kmsa, _existing, alias: ("Blazers of the Lakeside", AliasKind.Nickname));
        var changing = await SaveOrganisationAsync(App.Services, kmsa, "Kololo Kudus Club", type: OrganisationType.Community, city: "Entebbe");
        using (var added = await registrar.PostAsJsonAsync(OrganisationAliasesPath(changing.Id), new { name = alias, kind = "NICKNAME" }, Ct))
        {
            added.StatusCode.ShouldBe(HttpStatusCode.OK, because);
        }

        var body = new { type = "COMMUNITY", displayName = "Kololo Kudus Club", city = "Kampala" };
        using (var refused = await registrar.PutAsJsonAsync(OrganisationPath(changing.Id), body, Ct))
        {
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_DUPLICATE_REVIEW_REQUIRED");
            problem.GetProperty("candidates").EnumerateArray()
                .Select(c => (c.GetProperty("organisationId").GetGuid(), c.GetProperty("confidence").GetString(), EvidenceOf(c)))
                .ShouldBe([(existing.Id, "LIKELY", "SAME_NAME_KEY SAME_CITY")], because);
        }

        // The check before a create has no organisation of its own, so no alias is compared: it finds only the one
        // being changed (the same name and type), not the other.
        OrganisationCandidates(await CheckOrganisationAsync(registrar, body)).ShouldBe([(changing.Id, "LIKELY")], because);
    }

    [Fact]
    public async Task A_display_name_without_letters_or_digits_and_a_missing_type_are_refused()
    {
        var (_, registrar) = await TenantAsync();

        using var keyless = await registrar.PostAsJsonAsync(OrganisationDuplicateCheckPath, new { type = "CLUB", displayName = "..." }, Ct);
        Fields(await keyless.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_NAME_INVALID"))
            .ShouldBe([("displayName", "ORGANISATION_NAME_INVALID")]);
        using var typeless = await registrar.PostAsJsonAsync(OrganisationDuplicateCheckPath, new { displayName = _existing }, Ct);
        Fields(await typeless.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST"))
            .ShouldBe([("type", "VALIDATION_INVALID_REQUEST")]);
    }

    // An alias the organisation had: removed with a reason, as the remove action leaves it.
    private async Task AddRemovedAliasAsync(Guid tenantId, Guid organisationId, string name)
    {
        await using var scope = RegistryTestData.ScopeFor(App.Services, tenantId, out var context);
        var organisation = await context.Set<Organisation>().Include(o => o.Aliases).SingleAsync(o => o.Id == organisationId, Ct);
        var alias = organisation.AddAlias(name, AliasKind.FormerName).Value;
        organisation.RemoveAlias(alias.Id, "Never its name", SystemActor.Instance, RegistryTestData.Now).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync(Ct);
    }

    // "CODE" or "CODE:value", in the order answered.
    private static string EvidenceOf(JsonElement candidate) => string.Join(' ', candidate.GetProperty("evidence").EnumerateArray()
        .Select(e => e.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String
            ? $"{e.GetProperty("code").GetString()}:{value.GetString()}"
            : e.GetProperty("code").GetString()));

    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync()
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}
