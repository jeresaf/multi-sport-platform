using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Messaging;
using SportsPlatform.IntegrationTests.Persistence;
using SportsPlatform.IntegrationTests.Seeding;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// The Host under test: <c>Testing</c> environment, the container's role connections, a fake clock, the test
/// authentication scheme (<see cref="TestAuthentication"/>), a configurable authorization evaluator, a recording
/// email sender (<see cref="RecordingEmailSender"/>), the messaging test module and test seeders.
/// </summary>
public sealed class TestApp(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));

    public TelemetryCapture Telemetry { get; } = new();

    public TestAuthorizationEvaluator Authorization { get; } = new();

    public ConsumerLog Consumers { get; } = new();

    public SeedLog Seeds { get; } = new();

    public RecordingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        builder.UseEnvironment(TestEnvironment.Name);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddTestAuthentication();
            services.RemoveAll<IAuthorizationEvaluator>();
            services.AddSingleton<IAuthorizationEvaluator>(Authorization);
            Telemetry.AddTo(services);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

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
