using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Organisations;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>Requests to <c>/api/admin/v1/organisations</c> (S07 D19) and organisations saved directly for the tests.</summary>
internal static class OrganisationRequests
{
    public const string OrganisationsPath = "/api/admin/v1/organisations";

    public const string OrganisationDuplicateCheckPath = OrganisationsPath + "/duplicate-check";

    // Saved organisations get slugs no create proposes (from -1000), so a test's names never clash on them.
    private static int _nextSlug = 1000;

    public static string OrganisationPath(Guid organisationId) => $"{OrganisationsPath}/{organisationId}";

    /// <summary>The demo's multi-sport club (S07 Demo), with only the required fields.</summary>
    public static Dictionary<string, object?> NtindaSwifts() => new()
    {
        ["type"] = "CLUB",
        ["displayName"] = "Ntinda Swifts SC",
    };

    public static async Task<JsonElement> CreateOrganisationAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(OrganisationsPath, body, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    public static async Task<JsonElement> CheckOrganisationAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(OrganisationDuplicateCheckPath, body, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <summary>The candidates' IDs and confidences, in the order answered.</summary>
    public static List<(Guid, string?)> OrganisationCandidates(JsonElement answer) =>
    [
        .. answer.GetProperty("candidates").EnumerateArray()
            .Select(candidate => (candidate.GetProperty("organisationId").GetGuid(), candidate.GetProperty("confidence").GetString())),
    ];

    /// <summary>An organisation of <paramref name="tenantId"/>, saved without the warning (the test's starting point).</summary>
    public static async Task<Organisation> SaveOrganisationAsync(
        IServiceProvider services,
        Guid tenantId,
        string displayName,
        OrganisationType type = OrganisationType.Club,
        string? city = "Kampala",
        string? legalName = null,
        string? shortName = null,
        Guid? primaryVenueId = null)
    {
        await using var scope = RegistryTestData.ScopeFor(services, tenantId, out var context);
        var organisation = Organisation.Create(
            new OrganisationDetails(type, displayName, legalName, shortName, "UG", city, null, null, null, null, null, primaryVenueId),
            Slugs.WithSuffix(Slugs.Propose("organisation", displayName), Interlocked.Increment(ref _nextSlug)),
            OrganisationSource.Manual,
            SystemActor.Instance,
            RegistryTestData.Now);
        context.Add(organisation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return organisation;
    }
}
