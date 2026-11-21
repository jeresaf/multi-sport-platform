using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>Requests to <c>/api/admin/v1/venues</c> (S07 D19) and venues saved directly for the venue tests.</summary>
internal static class VenueRequests
{
    public const string VenuesPath = "/api/admin/v1/venues";

    public const string VenueDuplicateCheckPath = VenuesPath + "/duplicate-check";

    // Saved venues get slugs no create proposes (from -1000), so a test's names never clash on them.
    private static int _nextSlug = 1000;

    public static string VenuePath(Guid venueId) => $"{VenuesPath}/{venueId}";

    public static string VenueStatusPath(Guid venueId) => $"{VenuePath(venueId)}/status";

    public static string VenueStatusChangesPath(Guid venueId) => $"{VenuePath(venueId)}/status-changes";

    /// <summary>The names of a page of the venue directory, in the order answered.</summary>
    public static List<string?> VenueNames(this JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString())];

    /// <summary>The demo's new venue (S07 Demo), with only the required fields.</summary>
    public static Dictionary<string, object?> KiwatuleMeadow() => new()
    {
        ["name"] = "Kiwatule Meadow Ground",
        ["type"] = "SPORTS_GROUND",
        ["city"] = "Kampala",
        ["timeZone"] = "Africa/Kampala",
    };

    public static async Task<JsonElement> CreateVenueAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(VenuesPath, body, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    public static async Task<JsonElement> CheckVenueAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(VenueDuplicateCheckPath, body, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <summary>The candidates' IDs and confidences, in the order answered.</summary>
    public static List<(Guid, string?)> Candidates(JsonElement answer) =>
    [
        .. answer.GetProperty("candidates").EnumerateArray()
            .Select(candidate => (candidate.GetProperty("venueId").GetGuid(), candidate.GetProperty("confidence").GetString())),
    ];

    /// <summary>A venue of <paramref name="tenantId"/>, saved without the warning (the test's starting point).</summary>
    public static async Task<Venue> SaveVenueAsync(
        IServiceProvider services,
        Guid tenantId,
        string name,
        string city = "Kampala",
        GeoPoint? location = null,
        VenueStatus status = VenueStatus.Active,
        IReadOnlyList<string>? supportedVariants = null)
    {
        await using var scope = RegistryTestData.ScopeFor(services, tenantId, out var context);
        var venue = Venue.Create(
            new VenueDetails(name, null, VenueType.SportsGround, null, city, null, "UG", location, "Africa/Kampala", null, [], supportedVariants ?? []),
            Slugs.WithSuffix(Slugs.Propose("venue", name), Interlocked.Increment(ref _nextSlug)),
            VenueSource.Manual,
            SystemActor.Instance,
            RegistryTestData.Now);

        // Reached through the lifecycle, so the history knows what an archived venue was before.
        if (status != VenueStatus.Active)
        {
            venue.ChangeStatus(status, "Set up by the test", SystemActor.Instance, RegistryTestData.Now).IsSuccess.ShouldBeTrue();
        }

        context.Add(venue);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return venue;
    }
}
