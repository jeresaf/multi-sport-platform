using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Persistence;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Platform.Infrastructure;

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
        [typeof(PlatformAuditDbContext)] =
            "a platform service on sp_platform (AddPlatformServiceDbContext): not migrated, no tenant-owned entities",
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

    // The harness can't see them, and on sp_platform RLS wouldn't stop them either.
    [Fact]
    public void Platform_service_contexts_map_no_tenant_owned_entities()
    {
        using var scope = App.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformAuditDbContext>();

        context.Model.GetEntityTypes().Select(entityType => entityType.ClrType).ShouldContain(typeof(AuditEntry));
        context.Model.GetEntityTypes().Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType.Name)
            .ShouldBeEmpty();
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
