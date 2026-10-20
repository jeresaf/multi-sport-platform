using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Messaging;
using SportsPlatform.IntegrationTests.Persistence;
using SportsPlatform.IntegrationTests.Seeding;
using SportsPlatform.Modules.Identity.Features.Authorization;
using SportsPlatform.Modules.Identity.Features.Directory;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// The Host under test: <c>Testing</c> environment, the container's role connections, a fake clock, the test
/// authentication scheme (<see cref="TestAuthentication"/>), a configurable authorization evaluator (or Identity's
/// with the memberships in the database, <see cref="TestAuthorizationEvaluator.UseIdentityRoles"/>), a recording email sender
/// (<see cref="RecordingEmailSender"/>), the messaging test module and test seeders.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _settings;

    public TestApp(IReadOnlyDictionary<string, string?> settings)
    {
        _settings = settings;
        TestAuthentication.UseHttps(ClientOptions);
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));

    public TelemetryCapture Telemetry { get; } = new();

    public TestAuthorizationEvaluator Authorization { get; } = new();

    public ConsumerLog Consumers { get; } = new();

    public SeedLog Seeds { get; } = new();

    public RecordingEmailSender Emails { get; } = new();

    public CountingPasswordHasher PasswordHashes { get; } = new();

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        TestAuthentication.ConfigureTestClient(client);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(_settings).Build();

        builder.UseEnvironment(TestEnvironment.Name);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(_settings));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddTestAuthentication();
            services.RemoveAll<IAuthorizationEvaluator>();
            services.AddScoped<IdentityAuthorizationEvaluator>();
            services.AddScoped<IAuthorizationEvaluator>(provider => Authorization.UsesIdentityRoles
                ? Authorization.Recording(provider.GetRequiredService<IdentityAuthorizationEvaluator>())
                : Authorization);

            // With Identity's roles, memberships come from the database too (the identity directory), not X-Test-Tenants.
            services.RemoveAll<ITenantMembershipChecker>();
            services.AddSingleton<TestAuthentication.ClaimsMembershipChecker>();
            services.AddScoped<ITenantMembershipChecker>(provider => Authorization.UsesIdentityRoles
                ? provider.GetRequiredService<IdentityDirectory>()
                : provider.GetRequiredService<TestAuthentication.ClaimsMembershipChecker>());

            // Likewise who may work in a PROVISIONING tenant: Identity's setup roles, or every X-Test-Tenants member.
            services.RemoveAll<ITenantSetupAccess>();
            services.AddScoped<TenantSetupAccess>();
            services.AddScoped<ITenantSetupAccess>(provider => Authorization.UsesIdentityRoles
                ? provider.GetRequiredService<TenantSetupAccess>()
                : TestAuthentication.ClaimsSetupAccess.Instance);
            Telemetry.AddTo(services);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.RemoveAll<IPasswordHasher<User>>();
            services.AddSingleton<IPasswordHasher<User>>(PasswordHashes);

            // A test-only module context that exercises the persistence conventions.
            services.AddModuleDbContext<ConventionsTestDbContext>(configuration, ConventionsTestDbContext.SchemaName);

            // A test-only module that publishes and consumes, for the outbox and inbox.
            services.AddModuleDbContext<MessagingTestDbContext>(configuration, MessagingTestDbContext.SchemaName);
            services.AddMessagingTestHandlers(Consumers);

            // Test-only seeders, one or two per category, for the seed runner.
            services.AddTestSeeders(Seeds);
        });
    }
}
