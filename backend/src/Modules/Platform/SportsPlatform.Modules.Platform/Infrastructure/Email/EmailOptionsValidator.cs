using Microsoft.Extensions.Options;
using MimeKit;

namespace SportsPlatform.Modules.Platform.Infrastructure.Email;

/// <summary>
/// Refuses to start without a usable sender and server: a missing setting would otherwise surface only when the
/// first invitation fails to send. Messages name the setting, never its value.
/// </summary>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (!MailboxAddress.TryParse(options.From, out var from) || from.Address != options.From.Trim() || !from.Address.Contains('@', StringComparison.Ordinal))
        {
            failures.Add("Email:From must be one email address, e.g. no-reply@example.org.");
        }

        if (string.IsNullOrWhiteSpace(options.FromName))
        {
            failures.Add("Email:FromName must not be empty.");
        }

        var smtp = options.Smtp;
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            failures.Add("Email:Smtp:Host must be set.");
        }

        if (smtp.Port is < 1 or > 65535)
        {
            failures.Add("Email:Smtp:Port must be between 1 and 65535.");
        }

        if (!Enum.IsDefined(smtp.Security))
        {
            failures.Add("Email:Smtp:Security must be None, StartTls or SslOnConnect.");
        }

        if (smtp.TimeoutSeconds is < 1 or > 300)
        {
            failures.Add("Email:Smtp:TimeoutSeconds must be between 1 and 300.");
        }

        if (string.IsNullOrEmpty(smtp.UserName) != string.IsNullOrEmpty(smtp.Password))
        {
            failures.Add("Email:Smtp:UserName and Email:Smtp:Password must be set together.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
