namespace SportsPlatform.Modules.Platform.Infrastructure.Email;

/// <summary>
/// The sender and the SMTP server (<c>Email:*</c>). Development points at Mailpit; elsewhere the deployment sets
/// them (the provider is the infrastructure task's, AR-33). Validated on start (<see cref="EmailOptionsValidator"/>).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The sender's address, e.g. <c>no-reply@sportsplatform.localhost</c> (the domain waits for Q10).</summary>
    public string From { get; set; } = "";

    /// <summary>The sender's display name.</summary>
    public string FromName { get; set; } = "SportsPlatform";

    public SmtpOptions Smtp { get; set; } = new();
}

/// <summary>The SMTP server (<c>Email:Smtp:*</c>).</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    /// <summary>Authenticates only when set.</summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>How the connection is secured.</summary>
public enum SmtpSecurity
{
    /// <summary>Plain text: only for a local server such as Mailpit.</summary>
    None,

    /// <summary>Upgrades the connection with STARTTLS and refuses a server that can't (usually port 587).</summary>
    StartTls,

    /// <summary>TLS from the first byte (usually port 465).</summary>
    SslOnConnect,
}
