using System.Net.Http.Json;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>Requests to <c>/api/admin/v1/tenant/sports</c> (S05 D15, D16) as a user working in a tenant.</summary>
internal static class TenantSportRequests
{
    public const string TenantSportsPath = "/api/admin/v1/tenant/sports";

    public const string Football = "FOOTBALL_ASSOCIATION";

    public static Task<HttpResponseMessage> EnableSportAsync(this HttpClient client, string variant, CancellationToken cancellationToken) =>
        client.PostAsync($"{TenantSportsPath}/{variant}/enable", content: null, cancellationToken);

    public static Task<HttpResponseMessage> DisableSportAsync(
        this HttpClient client, string variant, CancellationToken cancellationToken, string? reason = "End of the schools season") =>
        client.PostAsJsonAsync($"{TenantSportsPath}/{variant}/disable", new { reason }, cancellationToken);
}
