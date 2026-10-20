using System.Text.Encodings.Web;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.Modules.Identity.Features.Invitations.SendInvitationEmail;

/// <summary>The invitation email (S03 D13): plain text and the same in simple HTML, English (Q7).</summary>
public static class InvitationEmail
{
    /// <summary>The admin web's page that reads the token and asks for a password or a sign-in (S03-T6).</summary>
    public const string PagePath = "accept-invitation";

    public static string Subject(string tenantName) => $"You're invited to join {tenantName}";

    public static Uri Link(string adminAppUrl, string token)
    {
        ArgumentNullException.ThrowIfNull(adminAppUrl);
        var baseUrl = new Uri(adminAppUrl.TrimEnd('/') + "/", UriKind.Absolute);
        return new Uri(baseUrl, $"{PagePath}?token={Uri.EscapeDataString(token)}");
    }

    public static EmailMessage Create(string to, string tenantName, Uri link)
    {
        ArgumentNullException.ThrowIfNull(tenantName);
        ArgumentNullException.ThrowIfNull(link);
        var days = (int)Invitation.Lifetime.TotalDays;
        var url = link.AbsoluteUri;
        var name = HtmlEncoder.Default.Encode(tenantName);

        var text =
            $"""
            You're invited to join {tenantName}.

            To accept, open this link within {days} days of the invitation:
            {url}

            The link works once. If you didn't expect this invitation, ignore this email.
            """;
        var html =
            $"""
            <p>You're invited to join {name}.</p>
            <p>To accept, open this link within {days} days of the invitation:<br><a href="{HtmlEncoder.Default.Encode(url)}">Accept the invitation</a></p>
            <p>The link works once. If you didn't expect this invitation, ignore this email.</p>
            """;
        return new EmailMessage(to, ToName: null, Subject(tenantName), text, html);
    }
}
