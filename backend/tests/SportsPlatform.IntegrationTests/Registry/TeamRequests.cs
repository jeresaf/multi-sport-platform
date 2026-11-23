using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Teams;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>Requests to <c>/api/admin/v1/teams</c> (S07 D19) and teams saved directly for the tests.</summary>
internal static class TeamRequests
{
    public const string TeamsPath = "/api/admin/v1/teams";

    public const string TeamDuplicateCheckPath = TeamsPath + "/duplicate-check";

    // Saved teams get slugs no create proposes (from -1000), so a test's names never clash on them.
    private static int _nextSlug = 1000;

    public static string TeamPath(Guid teamId) => $"{TeamsPath}/{teamId}";

    /// <summary>The demo club's first team (S07 Demo) in a test variant, with only the required fields.</summary>
    public static Dictionary<string, object?> NtindaSwiftsMen() => new()
    {
        ["variant"] = TestballFull,
        ["name"] = "Ntinda Swifts FC",
        ["genderCategory"] = "MEN",
        ["ageGroup"] = "SENIOR",
    };

    public static TeamCategory SeniorMen { get; } = new(GenderCategory.Men, AgeGroup.Senior, TeamLevel.First);

    public static async Task<JsonElement> CreateTeamAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(TeamsPath, body, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    public static async Task<JsonElement> CheckTeamAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(TeamDuplicateCheckPath, body, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <summary>The candidates' IDs and confidences, in the order answered.</summary>
    public static List<(Guid, string?)> TeamCandidates(JsonElement answer) =>
    [
        .. answer.GetProperty("candidates").EnumerateArray()
            .Select(candidate => (candidate.GetProperty("teamId").GetGuid(), candidate.GetProperty("confidence").GetString())),
    ];

    /// <summary>
    /// A team of <paramref name="tenantId"/>, saved without the warning or the quota (the test's starting point), in
    /// <paramref name="status"/> set directly (the lifecycle is S07-T3b's).
    /// </summary>
    public static async Task<Team> SaveTeamAsync(
        IServiceProvider services,
        Guid tenantId,
        string name,
        string variant = TestballFull,
        TeamCategory? category = null,
        Guid? organisationId = null,
        string? shortName = null,
        TeamStatus status = TeamStatus.Active)
    {
        await using var scope = RegistryTestData.ScopeFor(services, tenantId, out var context);
        category ??= SeniorMen;
        var team = Team.Create(
            variant,
            new TeamDetails(name, shortName, category, organisationId, null, null, null, null, null, null, null),
            new DateOnly(2020, 1, 1),
            Slugs.WithSuffix(Team.ProposeSlug(name, category), Interlocked.Increment(ref _nextSlug)),
            TeamSource.Manual,
            SystemActor.Instance,
            RegistryTestData.Now);
        context.Add(team);
        if (status != TeamStatus.Active)
        {
            context.Entry(team).Property(t => t.Status).CurrentValue = status;
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return team;
    }
}
