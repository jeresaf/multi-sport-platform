using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>Requests to <c>/api/admin/v1/people</c> (S06 D22) as a user working in a tenant.</summary>
internal static class PeopleRequests
{
    public const string PeoplePath = "/api/admin/v1/people";

    public const string DuplicateCheckPath = PeoplePath + "/duplicate-check";

    public static string PersonPath(Guid personId) => $"{PeoplePath}/{personId}";

    public static string PrivatePath(Guid personId) => $"{PeoplePath}/{personId}/private";

    public static string StatusPath(Guid personId, string transition) => $"{PeoplePath}/{personId}/{transition}";

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

    public static async Task<JsonElement> CheckAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(DuplicateCheckPath, body, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    public static async Task<string> CheckTokenAsync(HttpClient client, object body) =>
        (await CheckAsync(client, body)).GetProperty("checkToken").GetString()!;

    public static List<(string?, string?)> Fields(JsonElement problem) =>
    [
        .. problem.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("field").GetString(), error.GetProperty("code").GetString())),
    ];

    /// <summary>The body with one more member, or one replaced.</summary>
    public static Dictionary<string, object?> With(object body, string name, object? value)
    {
        var merged = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(body))!;
        merged[name] = value;
        return merged;
    }

    /// <summary>How many requests of this database wait on an advisory lock (the people lock, S06 D6).</summary>
    public static Task<int> WaitingOnAdvisoryLocksAsync(RegistryDbContext context) =>
        context.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())")
            .SingleAsync(TestContext.Current.CancellationToken);
}
