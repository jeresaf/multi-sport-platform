using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace SportsPlatform.Shared.Configuration;

public static partial class ConfigurationStartupValidator
{
    private static readonly string[] PlaceholderFragments =
    [
        "change-me",
        "developmentonly",
        "example",
        "replace-me",
        "replace-with",
        "yoursupersecret"
    ];

    public static void ValidateAndThrow(IConfiguration configuration, string environmentName)
    {
        var errors = GetErrors(configuration, environmentName);
        if (errors.Count == 0)
            return;

        throw new InvalidOperationException(
            "Application configuration is invalid:" + Environment.NewLine
            + string.Join(Environment.NewLine, errors.Select(error => $"- {error}")));
    }

    public static IReadOnlyList<string> GetErrors(
        IConfiguration configuration,
        string environmentName)
    {
        var errors = new List<string>();
        var protectedEnvironment = environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);
        var production = environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);

        if (production && configuration.GetValue<bool>("SeedData:EnableDemoData"))
            errors.Add("SeedData:EnableDemoData must be false in production.");

        var databaseConnection = Required(configuration, "Database:ConnectionString", errors);
        var tenantConnection = Required(configuration, "Tenant:DefaultConnectionString", errors);
        var jwtSecret = Required(configuration, "Jwt:Secret", errors);
        Required(configuration, "Jwt:Issuer", errors);
        Required(configuration, "Jwt:Audience", errors);

        if (!string.IsNullOrWhiteSpace(jwtSecret))
        {
            if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
                errors.Add("Jwt:Secret must contain at least 32 UTF-8 bytes.");
            if (production && LooksLikePlaceholder(jwtSecret))
                errors.Add("Jwt:Secret must not contain an example or placeholder value.");
        }

        ValidateConnectionString("Database:ConnectionString", databaseConnection, production, errors);
        ValidateConnectionString("Tenant:DefaultConnectionString", tenantConnection, production, errors);

        foreach (var previousSecret in configuration.GetSection("Jwt:PreviousSecrets").Get<string[]>()
                     ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(previousSecret))
                continue;
            if (Encoding.UTF8.GetByteCount(previousSecret) < 32 || LooksLikePlaceholder(previousSecret))
                errors.Add("Every Jwt:PreviousSecrets entry must be a non-placeholder value of at least 32 UTF-8 bytes.");
        }

        if (protectedEnvironment)
        {
            if (configuration.GetValue<bool>("Database:EnableSensitiveDataLogging"))
                errors.Add("Database:EnableSensitiveDataLogging must be false in staging and production.");

            Required(configuration, "Security:DataProtectionKeyPath", errors);
            Required(configuration, "Storage:Endpoint", errors);
            Required(configuration, "Storage:AccessKey", errors);
            var storageSecret = Required(configuration, "Storage:SecretKey", errors);
            Required(configuration, "Storage:Bucket", errors);
            Required(configuration, "Email:Host", errors);
            Required(configuration, "Email:FromAddress", errors);
            var identityWebBaseUrl = Required(configuration, "Email:AdminWebBaseUrl", errors);
            if (production && (!Uri.TryCreate(identityWebBaseUrl, UriKind.Absolute, out var identityUri)
                               || identityUri.Scheme != Uri.UriSchemeHttps))
                errors.Add("Email:AdminWebBaseUrl must be an absolute HTTPS URL in production.");

            if (production && !string.IsNullOrWhiteSpace(storageSecret) && LooksLikePlaceholder(storageSecret))
                errors.Add("Storage:SecretKey must not contain an example or placeholder value.");

            var databasePassword = ExtractPassword(databaseConnection);
            if (production && !string.IsNullOrWhiteSpace(databasePassword)
                && string.Equals(databasePassword, storageSecret, StringComparison.Ordinal))
            {
                errors.Add("Storage:SecretKey must not reuse the database password.");
            }

            var origins = configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
                ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
                .ToArray() ?? Array.Empty<string>();
            if (origins.Length == 0)
                errors.Add("Security:AllowedOrigins must contain at least one explicit web origin.");
            if (origins.Any(origin => origin == "*"))
                errors.Add("Security:AllowedOrigins must not contain a wildcard origin.");
            if (production && origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                                                   || uri.Scheme != Uri.UriSchemeHttps))
                errors.Add("Every production Security:AllowedOrigins entry must be an absolute HTTPS origin.");
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string Required(IConfiguration configuration, string key, ICollection<string> errors)
    {
        var value = configuration[key]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{key} is required.");
        return value;
    }

    private static void ValidateConnectionString(
        string key,
        string value,
        bool rejectWeakPassword,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (LooksLikePlaceholder(value))
            errors.Add($"{key} must not contain an example or placeholder value.");

        var password = ExtractPassword(value);
        if (rejectWeakPassword && (string.IsNullOrWhiteSpace(password)
                                   || password.Equals("password", StringComparison.OrdinalIgnoreCase)
                                   || password.Equals("postgres", StringComparison.OrdinalIgnoreCase)
                                   || password.Equals("postgres123", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"{key} must contain a non-default password in staging and production.");
        }
    }

    private static bool LooksLikePlaceholder(string value) =>
        PlaceholderFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string? ExtractPassword(string connectionString)
    {
        var match = PasswordPattern().Match(connectionString);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"(?:^|;)\s*Password\s*=\s*([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();
}
