using System.Text.Encodings.Web;
using SportsPlatform.Modules.Identity.Infrastructure.PasswordReset;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.Modules.Identity.Features.Account.ForgotPassword;

/// <summary>The password reset email (S03 D13): plain text and the same in simple HTML, English (Q7).</summary>
public static class PasswordResetEmail
{
    public const string Subject = "Reset your password";

    /// <summary>The admin web's page that reads the token and asks for the new password.</summary>
    public const string PagePath = "reset-password";

    public static Uri Link(string adminAppUrl, string token)
    {
        ArgumentNullException.ThrowIfNull(adminAppUrl);
        var baseUrl = new Uri(adminAppUrl.TrimEnd('/') + "/", UriKind.Absolute);
        return new Uri(baseUrl, $"{PagePath}?token={Uri.EscapeDataString(token)}");
    }

    public static EmailMessage Create(string to, Uri link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var hours = (int)PasswordResetTokenProvider.Lifetime.TotalHours;
        var lifetime = hours == 1 ? "1 hour" : $"{hours} hours";
        var url = link.AbsoluteUri;

        var text =
            $"""
            Someone asked to reset the password for your account.

            To choose a new password, open this link within {lifetime}:
            {url}

            The link works once. If you didn't ask for it, ignore this email: your password stays the same.
            """;
        var html =
            $"""
            <p>Someone asked to reset the password for your account.</p>
            <p>To choose a new password, open this link within {lifetime}:<br><a href="{HtmlEncoder.Default.Encode(url)}">Reset your password</a></p>
            <p>The link works once. If you didn't ask for it, ignore this email: your password stays the same.</p>
            """;
        return new EmailMessage(to, ToName: null, Subject, text, html);
    }
}
