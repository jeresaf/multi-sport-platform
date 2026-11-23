using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The team duplicate warning (S07 D13): only teams of the same variant and category are compared (owner, S07-T3a: a
/// club's other categories are other teams); <c>LIKELY</c> when the name or short name of one has a key of the other's;
/// <c>POSSIBLE</c> when the names are at least 0.7 similar and the two share an organisation or either has none; at most
/// ten, strongest first.
/// </summary>
public sealed class TeamDuplicateCheckTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    // The existing team every case is checked against: Bunga Breakers (short name "Breakers"), Senior Men, first team,
    // of a club. pg_trgm compares words' trigrams: "Bunga Breakers" and "Bunga Breakers Youth" share 14 of 20 (0.70),
    // "Bunga Breakers United" 14 of 21 (0.667).
    private const string _existing = "Bunga Breakers";

    [Theory]
    [InlineData("the same name in other words", "breakers,  BUNGA", null, "TESTBALL_FULL", "MEN", "SENIOR", null, "SAME", "LIKELY", "SAME_NAME_KEY SAME_ORGANISATION")]
    [InlineData("the same name, another organisation", "Bunga Breakers", null, "TESTBALL_FULL", "MEN", "SENIOR", null, "OTHER", "LIKELY", "SAME_NAME_KEY")]
    [InlineData("the same name, no organisation", "Bunga Breakers", null, "TESTBALL_FULL", "MEN", "SENIOR", null, null, "LIKELY", "SAME_NAME_KEY")]
    [InlineData("the name is its short name", "Breakers", null, "TESTBALL_FULL", "MEN", "SENIOR", null, "OTHER", "LIKELY", "SAME_NAME_KEY")]
    [InlineData("the short name is its name", "Lakeside Juniors", "Bunga Breakers", "TESTBALL_FULL", "MEN", "SENIOR", null, "OTHER", "LIKELY", "SAME_NAME_KEY")]
    [InlineData("the same name, another variant", "Bunga Breakers", null, "TESTBALL_SHORT", "MEN", "SENIOR", null, "SAME", null, null)]
    [InlineData("the same name, another gender", "Bunga Breakers", null, "TESTBALL_FULL", "WOMEN", "SENIOR", null, "SAME", null, null)]
    [InlineData("the same name, another age group", "Bunga Breakers", null, "TESTBALL_FULL", "MEN", "U17", null, "SAME", null, null)]
    [InlineData("the same name, another level", "Bunga Breakers", null, "TESTBALL_FULL", "MEN", "SENIOR", "RESERVE", "SAME", null, null)]
    [InlineData("names 0.70 similar, the same organisation", "Bunga Breakers Youth", null, "TESTBALL_FULL", "MEN", "SENIOR", "FIRST", "SAME", "POSSIBLE", "NAME_SIMILARITY:0.70 SAME_ORGANISATION")]
    [InlineData("names 0.70 similar, no organisation", "Bunga Breakers Youth", null, "TESTBALL_FULL", "MEN", "SENIOR", null, null, "POSSIBLE", "NAME_SIMILARITY:0.70")]
    [InlineData("names 0.70 similar, another organisation", "Bunga Breakers Youth", null, "TESTBALL_FULL", "MEN", "SENIOR", null, "OTHER", null, null)]
    [InlineData("names 0.667 similar, the same organisation", "Bunga Breakers United", null, "TESTBALL_FULL", "MEN", "SENIOR", null, "SAME", null, null)]
    [InlineData("names 0.70 similar, another category", "Bunga Breakers Youth", null, "TESTBALL_FULL", "MEN", "U17", null, "SAME", null, null)]
    public async Task The_warning_compares_names_within_the_same_variant_and_category(
        string because, string name, string? shortName, string variant, string genderCategory, string ageGroup, string? level,
        string? organisation, string? confidence, string? evidence)
    {
        var (kmsa, registrar) = await TenantAsync();
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Bunga Breakers SC");
        var other = await SaveOrganisationAsync(App.Services, kmsa, "Lakeside Juniors SC");
        var existing = await SaveTeamAsync(App.Services, kmsa, _existing, organisationId: club.Id, shortName: "Breakers");
        var organisationId = organisation switch
        {
            "SAME" => club.Id,
            "OTHER" => other.Id,
            _ => (Guid?)null,
        };

        var answer = await CheckTeamAsync(registrar, new { variant, name, shortName, genderCategory, ageGroup, level, organisationId });

        if (confidence is null)
        {
            TeamCandidates(answer).ShouldBeEmpty(because);
            return;
        }

        TeamCandidates(answer).ShouldBe([(existing.Id, confidence)], because);
        EvidenceOf(answer.GetProperty("candidates")[0]).ShouldBe(evidence, because);
    }

    // D13: an independent existing team (no organisation) is POSSIBLE for a similar name of any organisation.
    [Fact]
    public async Task An_independent_team_is_possible_for_a_similar_name_of_a_club()
    {
        var (kmsa, registrar) = await TenantAsync();
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Bunga Breakers SC");
        var independent = await SaveTeamAsync(App.Services, kmsa, _existing);

        var answer = await CheckTeamAsync(
            registrar, new { variant = TestballFull, name = "Bunga Breakers Youth", genderCategory = "MEN", ageGroup = "SENIOR", organisationId = club.Id });

        TeamCandidates(answer).ShouldBe([(independent.Id, "POSSIBLE")]);
    }

    [Fact]
    public async Task Candidates_come_strongest_first_at_most_ten_and_the_answer_has_what_the_form_shows()
    {
        var (kmsa, registrar) = await TenantAsync();
        for (var i = 0; i < 11; i++)
        {
            await SaveTeamAsync(App.Services, kmsa, "Bunga Breakers Youth");
        }

        var same = await SaveTeamAsync(App.Services, kmsa, _existing, status: TeamStatus.Archived);

        var answer = await CheckTeamAsync(registrar, new { variant = TestballFull, name = _existing, genderCategory = "MEN", ageGroup = "SENIOR" });

        var candidates = answer.GetProperty("candidates").EnumerateArray().ToList();
        candidates.Count.ShouldBe(10);
        candidates[0].EnumerateObject().Select(property => property.Name).ShouldBe(
            ["teamId", "name", "variant", "genderCategory", "ageGroup", "level", "organisationId", "status", "confidence", "evidence"]);
        (candidates[0].GetProperty("teamId").GetGuid(), candidates[0].GetProperty("name").GetString(), candidates[0].GetProperty("variant").GetString(),
                candidates[0].GetProperty("genderCategory").GetString(), candidates[0].GetProperty("ageGroup").GetString(),
                candidates[0].GetProperty("level").GetString(), candidates[0].GetProperty("status").GetString(),
                candidates[0].GetProperty("confidence").GetString())
            .ShouldBe((same.Id, _existing, TestballFull, "MEN", "SENIOR", "FIRST", "ARCHIVED", "LIKELY"));
        candidates.Skip(1).ShouldAllBe(candidate => candidate.GetProperty("confidence").GetString() == "POSSIBLE");
    }

    [Fact]
    public async Task A_name_without_letters_or_digits_and_a_missing_category_are_refused()
    {
        var (_, registrar) = await TenantAsync();

        using var keyless = await registrar.PostAsJsonAsync(
            TeamDuplicateCheckPath, new { variant = TestballFull, name = "...", genderCategory = "MEN", ageGroup = "SENIOR" }, Ct);
        Fields(await keyless.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_NAME_INVALID")).ShouldBe([("name", "TEAM_NAME_INVALID")]);
        using var categoryless = await registrar.PostAsJsonAsync(TeamDuplicateCheckPath, new { variant = TestballFull, name = _existing }, Ct);
        Fields(await categoryless.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST"))
            .ShouldBe([("genderCategory", "VALIDATION_INVALID_REQUEST"), ("ageGroup", "VALIDATION_INVALID_REQUEST")]);
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
