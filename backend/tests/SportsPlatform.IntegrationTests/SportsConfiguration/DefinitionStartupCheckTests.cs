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
    public void A_bad_format_stops_the_Host_at_startup()
    {
        using var app = new BadFormatApp();

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldContain("Format definitions are invalid");
        exception.Message.ShouldContain("knockout.yaml: $.stages[0].generator: unknown generator \"KNOCK_OUT\"");
    }

    [Fact]
    public void A_bad_ruleset_template_stops_the_Host_at_startup()
    {
        using var app = new BadApp(services => services.AddSingleton<RulesetTemplateFiles>(new UnknownFormat()));

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldContain("Ruleset templates are invalid");
        exception.Message.ShouldContain("football-league.yaml: $.formats[0]: unknown format \"LEAGUE\"");
    }

    [Fact]
    public void A_bad_tiebreaker_catalogue_stops_the_Host_at_startup()
    {
        using var app = new BadApp(services => services.AddSingleton<TiebreakerCatalogue>(new DuplicateTiebreaker()));

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldContain("Tiebreaker catalogue entries are invalid");
        exception.Message.ShouldContain("tiebreakers.yaml: $.tiebreakers[1].code: duplicate tiebreaker code \"POINTS\"");
    }

    [Fact]
    public void The_shipped_definitions_formats_and_templates_let_the_Host_start()
    {
        using var app = new HostOnlyApp();

        app.Services.GetRequiredService<SportDefinitionFiles>().Names.ShouldNotBeEmpty();
        app.Services.GetRequiredService<FormatDefinitionFiles>().Names.ShouldNotBeEmpty();
        app.Services.GetRequiredService<RulesetTemplateFiles>().Names.ShouldNotBeEmpty();
        app.Services.GetRequiredService<TiebreakerCatalogue>().All.ShouldNotBeEmpty();
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

    private sealed class BadFormatApp : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(TestEnvironment.Name);
            builder.ConfigureTestServices(services => services.AddSingleton<FormatDefinitionFiles>(new MisspeltGenerator()));
        }
    }

    private sealed class BadApp(Action<IServiceCollection> replace) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(TestEnvironment.Name);
            builder.ConfigureTestServices(replace);
        }
    }

    private sealed class UnknownFormat : RulesetTemplateFiles
    {
        public override string Read(string file)
        {
            var text = base.Read(file);
            return file == "football-league.yaml" ? text.Replace("formats: [ROUND_ROBIN,", "formats: [LEAGUE,", StringComparison.Ordinal) : text;
        }
    }

    private sealed class DuplicateTiebreaker : TiebreakerCatalogue
    {
        public override string Text() => base.Text().Replace("code: WINS,", "code: POINTS,", StringComparison.Ordinal);
    }

    private sealed class MisspeltGenerator : FormatDefinitionFiles
    {
        public override string Read(string file)
        {
            var text = base.Read(file);
            return file == "knockout.yaml" ? text.Replace("generator: KNOCKOUT", "generator: KNOCK_OUT", StringComparison.Ordinal) : text;
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
