using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Persistence;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>
/// The isolation harness finds entities through the <see cref="ModuleDatabase"/> registrations, so every
/// <see cref="ModuleDbContext"/> must have one (<c>AddModuleDbContext</c>) or be listed here with a reason.
/// </summary>
public sealed class ModuleDatabaseRegistrationTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Dictionary<Type, string> _unregistered = new()
    {
        [typeof(TenantOwnershipTests.KeyCheckContext)] = "a model-check fixture; never used against a database",
    };

    [Fact]
    public void Every_module_DbContext_is_registered_for_the_harness()
    {
        var registered = App.Services.GetServices<ModuleDatabase>().Select(database => database.ContextType).ToHashSet();
        var contexts = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("SportsPlatform.", StringComparison.Ordinal) == true)
            .SelectMany(LoadableTypes)
            .Where(type => type is { IsAbstract: false } && type.IsSubclassOf(typeof(ModuleDbContext)))
            .ToList();

        contexts.ShouldContain(typeof(ConventionsTestDbContext));
        contexts.Where(type => !registered.Contains(type) && !_unregistered.ContainsKey(type)).Select(type => type.FullName)
            .ShouldBeEmpty("module DbContexts the isolation harness would not see");
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
