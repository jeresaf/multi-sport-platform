using Microsoft.Extensions.Hosting;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>Validates every embedded sport definition when the Host starts; any problem stops it (S05 D3).</summary>
internal sealed class SportDefinitionStartupCheck(SportDefinitionFiles files) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var problems = files.Names.SelectMany(file => SportDefinitionValidator.Validate(file, files.Read(file))).ToList();
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Sport definitions are invalid:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
