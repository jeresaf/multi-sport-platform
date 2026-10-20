using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Features.Members;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>Requests to <c>/api/admin/v1/members</c> as a user working in a tenant (the real membership check and evaluator).</summary>
internal static class MemberRequests
{
    public const string MembersPath = "/api/admin/v1/members";

    public static JsonSerializerOptions Json => MessageSerialization.Options;

    public static string MemberPath(Guid membershipId) => $"{MembersPath}/{membershipId}";

    /// <summary>The user's client for the tenant: <c>X-Tenant-Id</c> is checked against the database, not <c>X-Test-Tenants</c>.</summary>
    public static HttpClient ClientFor(this TestApp app, Guid userId, Guid tenantId)
    {
        var client = app.CreateClient().AsUser(userId);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    public static Task<HttpResponseMessage> SuspendAsync(this HttpClient client, Guid membershipId, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{MemberPath(membershipId)}/suspend", new { reason = "Membership fees unpaid" }, cancellationToken);

    public static Task<HttpResponseMessage> RestoreAsync(this HttpClient client, Guid membershipId, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{MemberPath(membershipId)}/restore", new { }, cancellationToken);

    public static Task<HttpResponseMessage> RemoveAsync(this HttpClient client, Guid membershipId, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{MemberPath(membershipId)}/remove", new { reason = "Left the association" }, cancellationToken);

    public static Task<HttpResponseMessage> AssignAsync(
        this HttpClient client, Guid membershipId, string roleCode, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{MemberPath(membershipId)}/roles", new { roleCode }, cancellationToken);

    public static Task<HttpResponseMessage> RevokeAsync(
        this HttpClient client, Guid membershipId, Guid assignmentId, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{MemberPath(membershipId)}/roles/{assignmentId}/revoke", new { }, cancellationToken);

    public static async Task<MemberResponse> ReadMemberAsync(this HttpResponseMessage response, CancellationToken cancellationToken) =>
        (await response.Content.ReadFromJsonAsync<MemberResponse>(Json, cancellationToken)).ShouldNotBeNull();
}
