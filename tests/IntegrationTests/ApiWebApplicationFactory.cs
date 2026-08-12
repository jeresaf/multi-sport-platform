using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Identity.Services;

namespace SportsPlatform.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"sports-platform-{Guid.NewGuid()}";
    private readonly string? _postgresConnectionString;
    public TestIdentityMessageSender IdentityMessages { get; } = new();

    public ApiWebApplicationFactory()
    {
    }

    internal ApiWebApplicationFactory(string postgresConnectionString)
    {
        _postgresConnectionString = postgresConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "Jwt:Secret",
            "integration-test-secret-at-least-thirty-two-characters-long");
        builder.UseSetting("Jwt:Issuer", "SportsPlatform");
        builder.UseSetting("Jwt:Audience", "SportsPlatformUsers");
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenExpirationDays", "7");
        builder.UseSetting("SeedData:EnableDemoData", "true");
        builder.UseSetting("Security:RateLimits:GlobalPerMinute", "100000");
        builder.UseSetting("Security:RateLimits:AuthenticationPerMinute", "10000");
        builder.UseSetting(
            "Database:ConnectionString",
            _postgresConnectionString ?? "unused-in-memory");
        builder.UseSetting(
            "Tenant:DefaultConnectionString",
            _postgresConnectionString ?? "unused-in-memory");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIdentityMessageSender>();
            services.AddSingleton<IIdentityMessageSender>(IdentityMessages);
            services.RemoveAll<DbContextOptions<SportsPlatformDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SportsPlatformDbContext>>();
            services.RemoveAll<SportsPlatformDbContext>();
            services.AddDbContext<SportsPlatformDbContext>(options =>
            {
                if (_postgresConnectionString == null)
                    options.UseInMemoryDatabase(_databaseName);
                else
                    options.UseNpgsql(_postgresConnectionString);
            });
        });
    }
}
