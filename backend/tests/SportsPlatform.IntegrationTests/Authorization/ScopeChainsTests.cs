using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>
/// The scope chain registry (S03-T5): a resource's chain is the resource and the scopes its module's provider puts above
/// it (a contest → its competition; the tenant is reached by tenant-wide assignments); one provider per resource type.
/// </summary>
public sealed class ScopeChainsTests
{
    private static readonly Guid _metroFootballLeague = new("0192a1b2-0000-7000-8000-00000000c001");
    private static readonly Guid _metroFootballLeagueOpener = new("0192a1b2-0000-7000-8000-00000000d001");
    private static readonly Guid _ntindaSwifts = new("0192a1b2-0000-7000-8000-00000000e001");

    private static readonly ScopeRef _competition = new(AssignmentScope.Competition, _metroFootballLeague);
    private static readonly ScopeRef _contest = new(AssignmentScope.Contest, _metroFootballLeagueOpener);

    [Fact]
    public async Task A_contests_chain_is_the_contest_and_the_scopes_above_it()
    {
        var chains = new ScopeChains([new FakeScopeChain(AssignmentScope.Contest, _metroFootballLeagueOpener, _competition)]);

        (await chains.ChainAsync(_contest, TestContext.Current.CancellationToken)).ShouldBe([_contest, _competition], ignoreOrder: true);
    }

    [Fact]
    public async Task A_resource_without_a_provider_or_unknown_to_it_is_its_own_chain()
    {
        var chains = new ScopeChains([new FakeScopeChain(AssignmentScope.Contest, _metroFootballLeagueOpener, _competition)]);
        var team = new ScopeRef(AssignmentScope.Team, _ntindaSwifts);
        var otherContest = new ScopeRef(AssignmentScope.Contest, _ntindaSwifts);

        (await chains.ChainAsync(team, TestContext.Current.CancellationToken)).ShouldBe([team]);
        (await chains.ChainAsync(otherContest, TestContext.Current.CancellationToken)).ShouldBe([otherContest]);
    }

    [Fact]
    public void Two_providers_for_one_resource_type_stop_the_Host_at_startup()
    {
        using var app = new HostOnlyApp().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IScopeChainProvider>(_ => new FakeScopeChain(AssignmentScope.Contest, _metroFootballLeagueOpener, _competition));
            services.AddScoped<IScopeChainProvider>(_ => new FakeScopeChain(AssignmentScope.Contest, _metroFootballLeagueOpener));
            services.AddScoped<IScopeChainProvider>(_ => new FakeScopeChain(AssignmentScope.Team, _ntindaSwifts));
        }));

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldBe(
            "Each resource type has at most one scope chain provider:\nResource type CONTEST has 2 scope chain providers: FakeScopeChain, FakeScopeChain.");
    }
}
