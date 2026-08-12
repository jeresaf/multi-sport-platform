using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SportsPlatform.Shared.Configuration;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class ConfigurationStartupValidatorTests
{
    [Fact]
    public void Missing_required_values_fail_with_key_names_but_not_secret_values()
    {
        var configuration = Build(new Dictionary<string, string?>());

        var action = () => ConfigurationStartupValidator.ValidateAndThrow(configuration, "Development");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Database:ConnectionString is required.*")
            .WithMessage("*Tenant:DefaultConnectionString is required.*")
            .WithMessage("*Jwt:Secret is required.*");
    }

    [Fact]
    public void Production_rejects_placeholders_secret_reuse_and_missing_protections()
    {
        const string reusedSecret = "replace-with-one-shared-secret-value";
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = $"Host=db;Database=sports;Username=app;Password={reusedSecret}",
            ["Tenant:DefaultConnectionString"] = $"Host=db;Database=sports;Username=app;Password={reusedSecret}",
            ["Jwt:Secret"] = "replace-with-a-signing-secret-over-thirty-two-bytes",
            ["Jwt:Issuer"] = "SportsPlatform",
            ["Jwt:Audience"] = "SportsPlatformUsers",
            ["Storage:Endpoint"] = "https://objects.internal",
            ["Storage:AccessKey"] = "sports-platform",
            ["Storage:SecretKey"] = reusedSecret,
            ["Storage:Bucket"] = "sports-platform",
            ["Security:AllowedOrigins:0"] = "http://localhost:3000",
            ["Database:EnableSensitiveDataLogging"] = "true",
            ["SeedData:EnableDemoData"] = "true"
        });

        var errors = ConfigurationStartupValidator.GetErrors(configuration, "Production");

        errors.Should().Contain(error => error.Contains("placeholder", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("must not reuse", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("DataProtectionKeyPath", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("HTTPS", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("SensitiveDataLogging", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("EnableDemoData", StringComparison.Ordinal));
        string.Join(' ', errors).Should().NotContain(reusedSecret);
    }

    [Fact]
    public void Production_accepts_complete_distinct_https_configuration()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = "Host=db;Database=sports;Username=app;Password=db-strong-secret-2026",
            ["Tenant:DefaultConnectionString"] = "Host=db;Database=sports;Username=app;Password=db-strong-secret-2026",
            ["Jwt:Secret"] = "jwt-signing-value-with-at-least-thirty-two-bytes-2026",
            ["Jwt:Issuer"] = "SportsPlatform",
            ["Jwt:Audience"] = "SportsPlatformUsers",
            ["Storage:Endpoint"] = "https://objects.internal",
            ["Storage:AccessKey"] = "sports-platform",
            ["Storage:SecretKey"] = "object-storage-distinct-secret-2026",
            ["Storage:Bucket"] = "sports-platform",
            ["Email:Host"] = "smtp.internal",
            ["Email:FromAddress"] = "identity@sports.example",
            ["Email:AdminWebBaseUrl"] = "https://admin.sports.example",
            ["Security:DataProtectionKeyPath"] = "/run/secrets/data-protection",
            ["Security:AllowedOrigins:0"] = "https://admin.sports.example",
            ["Security:AllowedOrigins:1"] = "https://sports.example"
        });

        ConfigurationStartupValidator.GetErrors(configuration, "Production").Should().BeEmpty();
    }

    private static IConfiguration Build(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
