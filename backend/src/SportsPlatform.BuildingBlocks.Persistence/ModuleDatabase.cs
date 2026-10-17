namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>A module's schema and DbContext, as seen by the migration runner.</summary>
public sealed class ModuleDatabase(string schema, Type contextType, Func<string, ModuleDbContext> createContext)
{
    public string Schema { get; } = schema;

    public Type ContextType { get; } = contextType;

    /// <summary>Creates the context on the given connection string, without interceptors.</summary>
    public ModuleDbContext CreateContext(string connectionString) => createContext(connectionString);
}
