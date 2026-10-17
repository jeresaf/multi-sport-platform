// Developer and operations CLI. Lists the commands named in the slice specs; each is implemented by the slice
// that needs it.
(string Name, string Description)[] commands =
[
    ("migrate", "Apply database migrations for every module"),
    ("seed", "Run seeders for a category"),
    ("export-openapi", "Write contracts/openapi.json"),
    ("create-platform-admin", "Create the first platform administrator"),
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
