using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using SportsPlatform.Modules.Identity.Services;

namespace SportsPlatform.Api.Services;

public sealed class SmtpIdentityMessageSender : IIdentityMessageSender
{
    private readonly IConfiguration _configuration;

    public SmtpIdentityMessageSender(IConfiguration configuration) =>
        _configuration = configuration;

    public Task SendPasswordResetAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        var link = BuildLink("reset-password", email, token);
        return SendAsync(
            email,
            "Reset your Sports Platform password",
            $"Use this link to reset your password: {link}",
            cancellationToken);
    }

    public Task SendEmailVerificationAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        var link = BuildLink("verify-email", null, token);
        return SendAsync(
            email,
            "Verify your Sports Platform email",
            $"Use this link to verify your email address: {link}",
            cancellationToken);
    }

    private string BuildLink(string path, string? email, string token)
    {
        var baseUrl = (_configuration["Email:AdminWebBaseUrl"]
            ?? "http://localhost:3000").TrimEnd('/');
        var query = email is null
            ? $"token={UrlEncoder.Default.Encode(token)}"
            : $"email={UrlEncoder.Default.Encode(email)}&token={UrlEncoder.Default.Encode(token)}";
        return $"{baseUrl}/{path}?{query}";
    }

    private async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var host = _configuration["Email:Host"]
            ?? throw new InvalidOperationException("Email:Host is required.");
        var port = _configuration.GetValue("Email:Port", 25);
        var fromAddress = _configuration["Email:FromAddress"]
            ?? throw new InvalidOperationException("Email:FromAddress is required.");
        using var message = new MailMessage(fromAddress, recipient, subject, body);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = _configuration.GetValue<bool>("Email:UseTls"),
            UseDefaultCredentials = false
        };
        var username = _configuration["Email:Username"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, _configuration["Email:Password"]);
        await client.SendMailAsync(message, cancellationToken);
    }
}
