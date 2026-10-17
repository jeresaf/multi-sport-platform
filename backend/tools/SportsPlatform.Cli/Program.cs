// Developer and operations CLI. Commands are listed here as they are planned; each is implemented by the slice
// that needs it (docs/architecture/overview.md).
(string Name, string Description)[] commands =
[
    ("migrate", "Apply database migrations for every module (S01)"),
    ("seed", "Run seeders for an environment (S01)"),
    ("create-platform-admin", "Create the first platform administrator (S03)"),
    ("export-catalogues", "Export actions, errors and events to docs/generated and contracts (S01)"),
];

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    PrintUsage();
    return 0;
}

var command = Array.Find(commands, c => c.Name == args[0]);
if (command.Name is not null)
{
    Console.Error.WriteLine($"Command '{command.Name}' is not implemented yet.");
    return 1;
}

Console.Error.WriteLine($"Unknown command '{args[0]}'.");
PrintUsage();
return 1;

void PrintUsage()
{
    Console.WriteLine("Usage: dotnet run --project backend/tools/SportsPlatform.Cli -- <command>");
    Console.WriteLine();
    Console.WriteLine("Commands (not implemented yet):");
    foreach (var (name, description) in commands)
    {
        Console.WriteLine($"  {name,-24}{description}");
    }
}
