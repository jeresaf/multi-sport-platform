using Microsoft.Extensions.Hosting;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// Validates every embedded sport definition, format, ruleset template and the tiebreaker catalogue when the Host
/// starts; any problem stops it (S05 D3). It runs in
/// <see cref="StartingAsync"/>, before any hosted service (the outbox processor among them) starts.
/// </summary>
internal sealed class DefinitionStartupCheck(
    SportDefinitionFiles sports,
    FormatDefinitionFiles formats,
    RulesetTemplateFiles templates,
    TiebreakerCatalogue tiebreakers) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        Check("Sport definitions", sports.Names.SelectMany(file => SportDefinitionValidator.Validate(file, sports.Read(file))));
        Check("Format definitions", formats.Names.SelectMany(file => FormatDefinitionValidator.Validate(file, formats.Read(file))));
        Check("Tiebreaker catalogue entries", tiebreakers.Validate());
        var references = RulesetReferences.FromFiles(sports, formats, tiebreakers);
        Check("Ruleset templates", templates.Names.SelectMany(file => RulesetTemplateValidator.Validate(file, templates.Read(file), references)));
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void Check(string kind, IEnumerable<DefinitionProblem> problems)
    {
        var all = problems.ToList();
        if (all.Count > 0)
        {
            throw new InvalidOperationException($"{kind} are invalid:{Environment.NewLine}{string.Join(Environment.NewLine, all)}");
        }
    }
}
