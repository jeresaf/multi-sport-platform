// Developer and operations CLI. Lists the commands named in the slice specs; each is implemented by the slice
// that needs it. Configuration comes from the Host's appsettings*.json and environment variables
// (e.g. ConnectionStrings__Migrator); `dotnet run` uses the Development environment (launchSettings.json),
// a published build defaults to Production.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Host;

(string Name, string Description, Func<string[], Task<int>>? Run)[] commands =
[
    ("migrate", "Apply database migrations for every module (as sp_migrator)", MigrateAsync),
    ("seed", "Run seeders for a category", null),
    ("export-openapi", "Write contracts/openapi.json", null),
    ("create-platform-admin", "Create the first platform administrator", null),
];

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    PrintUsage();
    return 0;
}

var command = Array.Find(commands, c => c.Name == args[0]);
if (command.Name is null)
{
    Console.Error.WriteLine($"Unknown command '{args[0]}'.");
    PrintUsage();
    return 1;
}

if (command.Run is null)
{
    Console.Error.WriteLine($"Command '{command.Name}' is not implemented yet.");
    return 1;
}

return await command.Run(args[1..]);

static async Task<int> MigrateAsync(string[] args)
{
    // appsettings*.json sit next to the CLI, not in the directory it is run from.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });
    builder.Services.AddSportsPlatform(builder.Configuration);
    using var host = builder.Build();

    await host.Services.GetRequiredService<MigrationRunner>().RunAsync();
    return 0;
}

void PrintUsage()
{
    Console.WriteLine("Usage: dotnet run --project backend/tools/SportsPlatform.Cli -- <command>");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    foreach (var (name, description, run) in commands)
    {
        var suffix = run is null ? " (not implemented yet)" : string.Empty;
        Console.WriteLine($"  {name,-24}{description}{suffix}");
    }
}
