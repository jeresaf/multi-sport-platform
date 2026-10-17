// Developer and operations CLI. Lists the commands named in the slice specs; each is implemented by the slice
// that needs it. Configuration comes from the Host's appsettings*.json and environment variables
// (e.g. ConnectionStrings__Migrator); `dotnet run` uses the Development environment (launchSettings.json),
// a published build defaults to Production.
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Host;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.Host.OpenApi;

(string Name, string Description, Func<string[], Task<int>>? Run)[] commands =
[
    ("migrate", "Apply database migrations for every module (as sp_migrator)", MigrateAsync),
    ("seed", "Run seeders for a category", null),
    ("export-catalogues", "Write the error catalogue to docs/generated and contracts", ExportCataloguesAsync),
    ("export-openapi", "Write contracts/openapi.json", ExportOpenApiAsync),
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

// Generated files are written from code only; CI (S01-T3c) regenerates them and fails when they are stale.
static async Task<int> ExportCataloguesAsync(string[] args)
{
    var root = FindRepositoryRoot(Directory.GetCurrentDirectory());
    if (root is null)
    {
        Console.Error.WriteLine("Run this inside the repository (no backend/SportsPlatform.slnx above the current directory).");
        return 1;
    }

    var errors = ErrorCatalogue.Build(ModuleRegistry.All);
    await WriteGeneratedAsync(Path.Combine(root, "docs", "generated", "errors.md"), ErrorCatalogue.ToMarkdown(errors));
    await WriteGeneratedAsync(Path.Combine(root, "contracts", "errors.json"), ErrorCatalogue.ToJson(errors));
    return 0;
}

// Builds the API (services and endpoints, as the Host maps them) without starting it: no server, no database.
static async Task<int> ExportOpenApiAsync(string[] args)
{
    var root = FindRepositoryRoot(Directory.GetCurrentDirectory());
    if (root is null)
    {
        Console.Error.WriteLine("Run this inside the repository (no backend/SportsPlatform.slnx above the current directory).");
        return 1;
    }

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });
    await using var app = SportsPlatformApp.Build(builder);

    await WriteGeneratedAsync(Path.Combine(root, "contracts", "openapi.json"), await ApiDocumentation.ExportAsync(app.Services));
    return 0;
}

static async Task WriteGeneratedAsync(string path, string content)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await File.WriteAllTextAsync(path, content);
    Console.WriteLine($"Wrote {path}");
}

static string? FindRepositoryRoot(string start)
{
    for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "backend", "SportsPlatform.slnx")))
        {
            return directory.FullName;
        }
    }

    return null;
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
