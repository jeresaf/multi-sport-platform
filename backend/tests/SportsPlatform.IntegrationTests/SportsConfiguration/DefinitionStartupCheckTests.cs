using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

public sealed class DefinitionStartupCheckTests
{
    [Fact]
    public void A_bad_sport_definition_stops_the_Host_at_startup()
    {
        using var app = new BadDefinitionApp();

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldContain("Sport definitions are invalid");
        exception.Message.ShouldContain("football.yaml: $.event_types[0].actors[0].role: unknown actor role \"SCORERR\"");
    }

    [Fact]
    public void The_shipped_definitions_let_the_Host_start()
    {
        using var app = new HostOnlyApp();

        app.Services.GetRequiredService<SportDefinitionFiles>().Names.ShouldNotBeEmpty();
    }

    // The Host without a database (the check runs before anything connects), serving football with a misspelt role.
    private sealed class BadDefinitionApp : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(TestEnvironment.Name);
            builder.ConfigureTestServices(services => services.AddSingleton<SportDefinitionFiles>(new MisspeltRole()));
        }
    }

    private sealed class MisspeltRole : SportDefinitionFiles
    {
        public override string Read(string file)
        {
            var text = base.Read(file);
            const string original = "- { role: SCORER, required_from: STANDARD }";
            text.ShouldContain(original);
            return text.Replace(original, "- { role: SCORERR, required_from: STANDARD }", StringComparison.Ordinal);
        }
    }
}
