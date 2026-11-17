using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>Requests to <c>/api/admin/v1/people</c> (S06 D22) as a user working in a tenant.</summary>
internal static class PeopleRequests
{
    public const string PeoplePath = "/api/admin/v1/people";

    public const string DuplicateCheckPath = PeoplePath + "/duplicate-check";

    public static string PersonPath(Guid personId) => $"{PeoplePath}/{personId}";

    public static string PrivatePath(Guid personId) => $"{PeoplePath}/{personId}/private";

    public static string StatusChangesPath(Guid personId) => $"{PeoplePath}/{personId}/status-changes";

    public static async Task<JsonElement> ReadJsonAsync(this HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>The display names of a page of the directory, in the order answered.</summary>
    public static List<string?> Names(this JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("displayName").GetString())];
}
