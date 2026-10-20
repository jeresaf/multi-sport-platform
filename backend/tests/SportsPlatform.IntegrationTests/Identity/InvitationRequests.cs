using System.Net.Http.Json;
using System.Text.RegularExpressions;
using SportsPlatform.Modules.Identity.Features.Invitations;
using SportsPlatform.Modules.Platform.Contracts.Email;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>Requests to <c>/api/admin/v1/invitations</c> and the platform owner invitation, and the emailed tokens.</summary>
internal static partial class InvitationRequests
{
    public const string InvitationsPath = "/api/admin/v1/invitations";

    public static string InvitationPath(Guid invitationId) => $"{InvitationsPath}/{invitationId}";

    public static string OwnerInvitationsPath(Guid tenantId) => $"/api/platform/v1/tenants/{tenantId}/owner-invitations";

    public static Task<HttpResponseMessage> InviteAsync(
        this HttpClient client, string email, CancellationToken cancellationToken, params string[] roleCodes) =>
        client.PostAsJsonAsync(InvitationsPath, new { email, roleCodes }, cancellationToken);

    public static Task<HttpResponseMessage> RevokeInvitationAsync(this HttpClient client, Guid invitationId, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{InvitationPath(invitationId)}/revoke", new { }, cancellationToken);

    public static async Task<InvitationResponse> ReadInvitationAsync(this HttpResponseMessage response, CancellationToken cancellationToken) =>
        (await response.Content.ReadFromJsonAsync<InvitationResponse>(Json, cancellationToken)).ShouldNotBeNull();

    /// <summary>Invites and expects 201; returns the invitation.</summary>
    public static async Task<InvitationResponse> InvitedAsync(
        this HttpClient client, string email, CancellationToken cancellationToken, params string[] roleCodes)
    {
        using var response = await client.InviteAsync(email, cancellationToken, roleCodes);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
        return await response.ReadInvitationAsync(cancellationToken);
    }

    /// <summary>The token in an invitation email's link.</summary>
    public static string TokenOf(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var match = AcceptLink().Match(message.TextBody);
        match.Success.ShouldBeTrue("The email has no invitation link.");
        return Uri.UnescapeDataString(match.Groups["token"].Value);
    }

    [GeneratedRegex(@"/accept-invitation\?token=(?<token>[^\s&]+)")]
    private static partial Regex AcceptLink();
}
