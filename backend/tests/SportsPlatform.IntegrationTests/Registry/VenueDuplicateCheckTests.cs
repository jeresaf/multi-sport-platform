using System.Net;
using System.Net.Http.Json;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The venue duplicate warning (S07 D13): the same name in the same city, coordinates within 200 m, names 0.7 similar
/// in the same city, and their edges; archived venues found and marked; a blank city never matches; at most ten.
/// </summary>
public sealed class VenueDuplicateCheckTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    // 0.35° N: one degree of latitude is about 111,195 m, so 0.001790° is 199 m and 0.001808° is 201 m.
    private static readonly GeoPoint _kiwatule = new(0.350000m, 32.600000m);

    [Fact]
    public async Task The_same_name_in_the_same_city_is_likely_and_in_another_city_is_nothing()
    {
        var (kmsa, registrar) = await TenantAsync();
        var kampala = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", city: "Mukono");

        var answer = await CheckVenueAsync(registrar, new { name = "Ground,  kiwatule MEADOW", city = " KAMPALA " });

        Candidates(answer).ShouldBe([(kampala.Id, "LIKELY")]);
    }

    // T1 acceptance: the edge at 0.7. "Kisaasi Field" and "Kisaasi Field Annex" share 14 of 20 trigrams (0.7);
    // "Kisaasi Field Annexe" 14 of 21 (0.667).
    [Fact]
    public async Task Names_at_least_0_7_similar_in_the_same_city_are_possible()
    {
        var (kmsa, registrar) = await TenantAsync();
        var field = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Field");

        var atEdge = await CheckVenueAsync(registrar, new { name = "Kisaasi Field Annex", city = "Kampala" });
        var below = await CheckVenueAsync(registrar, new { name = "Kisaasi Field Annexe", city = "Kampala" });
        var elsewhere = await CheckVenueAsync(registrar, new { name = "Kisaasi Field Annex", city = "Wakiso" });

        Candidates(atEdge).ShouldBe([(field.Id, "POSSIBLE")]);
        var evidence = atEdge.GetProperty("candidates")[0].GetProperty("evidence");
        (evidence[0].GetProperty("code").GetString(), evidence[0].GetProperty("value").GetString()).ShouldBe(("NAME_SIMILARITY", "0.70"));
        evidence[1].GetProperty("code").GetString().ShouldBe("SAME_CITY");
        Candidates(below).ShouldBeEmpty();
        Candidates(elsewhere).ShouldBeEmpty();
    }

    // T1 acceptance: the edge at 200 m, whatever the name and the city.
    [Fact]
    public async Task Coordinates_within_200_m_are_likely_whatever_the_name()
    {
        var (kmsa, registrar) = await TenantAsync();
        var near = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium", city: "Wakiso", location: new GeoPoint(0.351790m, 32.600000m));
        await SaveVenueAsync(App.Services, kmsa, "Ntinda Grounds", location: new GeoPoint(0.351808m, 32.600000m));
        await SaveVenueAsync(App.Services, kmsa, "Naguru Courts");

        var answer = await CheckVenueAsync(registrar, new { name = "Kiwatule Meadow Ground", city = "Kampala", latitude = _kiwatule.Latitude, longitude = _kiwatule.Longitude });

        Candidates(answer).ShouldBe([(near.Id, "LIKELY")]);
        var evidence = answer.GetProperty("candidates")[0].GetProperty("evidence").EnumerateArray().ShouldHaveSingleItem();
        (evidence.GetProperty("code").GetString(), evidence.GetProperty("value").GetString()).ShouldBe(("NEARBY", "199"));
    }

    [Fact]
    public async Task Archived_venues_are_found_and_marked_strongest_first_at_most_ten()
    {
        var (kmsa, registrar) = await TenantAsync();
        var archived = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Field", status: VenueStatus.Archived);
        for (var i = 0; i < 11; i++)
        {
            await SaveVenueAsync(App.Services, kmsa, "Kisaasi Field Annex");
        }

        var answer = await CheckVenueAsync(registrar, new { name = "Kisaasi Field", city = "Kampala" });

        var candidates = answer.GetProperty("candidates").EnumerateArray().ToList();
        candidates.Count.ShouldBe(10);
        (candidates[0].GetProperty("venueId").GetGuid(), candidates[0].GetProperty("status").GetString(), candidates[0].GetProperty("confidence").GetString())
            .ShouldBe((archived.Id, "ARCHIVED", "LIKELY"));
        candidates.Skip(1).ShouldAllBe(candidate => candidate.GetProperty("confidence").GetString() == "POSSIBLE");
    }

    [Fact]
    public async Task A_city_without_letters_or_digits_never_matches_and_a_name_without_them_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync();
        await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", city: "--");

        (await CheckVenueAsync(registrar, new { name = "Kiwatule Meadow Ground", city = "--" })).GetProperty("candidates").GetArrayLength().ShouldBe(0);
        using var refused = await registrar.PostAsJsonAsync(VenueDuplicateCheckPath, new { name = "...", city = "Kampala" }, Ct);
        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_NAME_INVALID")).ShouldBe([("name", "VENUE_NAME_INVALID")]);
        using var oneCoordinate = await registrar.PostAsJsonAsync(VenueDuplicateCheckPath, new { name = "Kisaasi Field", city = "Kampala", latitude = 0.35 }, Ct);
        Fields(await oneCoordinate.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST"))
            .ShouldBe([("longitude", "VALIDATION_INVALID_REQUEST")]);
    }

    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync()
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}
