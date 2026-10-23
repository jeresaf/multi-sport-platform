using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.Modules.Platform;

namespace SportsPlatform.IntegrationTests.Catalogues;

public sealed class ActionCatalogueTests
{
    // What the catalogue scans, plus BuildingBlocks, the Contracts and the Host, where a declared action would be missed.
    private static IEnumerable<Assembly> ScannedAssemblies =>
    [
        typeof(ActionDefinition).Assembly,
        typeof(ActionCatalogue).Assembly,
        .. ModuleRegistry.All.Select(module => module.GetType().Assembly),
        .. ModuleRegistry.All.Select(module => Assembly.Load($"{module.GetType().Assembly.GetName().Name}.Contracts")),
    ];

    // S01-T4 acceptance: the catalogue lists the Platform actions with module, scope, audit and entitlement fields.
    [Fact]
    public void Catalogue_lists_the_Platform_actions()
    {
        var catalogue = ActionCatalogue.Build(ModuleRegistry.All);

        catalogue.ShouldContain(new ActionCatalogueEntry(PlatformActions.OutboxRead, "Platform"));
        catalogue.ShouldContain(new ActionCatalogueEntry(PlatformActions.OutboxRedrive, "Platform"));
        PlatformActions.OutboxRead.Scope.ShouldBe(ActionScope.Platform);
        PlatformActions.OutboxRead.Audit.ShouldBe(AuditRequirement.Required);
        PlatformActions.OutboxRedrive.Audit.ShouldBe(AuditRequirement.RequiredWithReason);
        catalogue.Select(entry => entry.Action.Code.Value).ShouldBe(catalogue.Select(entry => entry.Action.Code.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Markdown_and_JSON_carry_every_field()
    {
        var catalogue = ActionCatalogue.Build(ModuleRegistry.All);

        var markdown = ActionCatalogue.ToMarkdown(catalogue);
        var json = ActionCatalogue.ToJson(catalogue);

        markdown.ShouldContain("| `platform.outbox.redrive` | Platform | PLATFORM | — | REQUIRED_WITH_REASON | — | no | no | no | no | S01 | Re-deliver failed outbox messages. |\n");
        markdown.ShouldContain("| `tenant.access.read` | Tenancy | TENANT | TENANT | REQUIRED | — | no | no | yes | yes | S03 |");
        markdown.ShouldNotContain("\r");
        json.ShouldNotContain("\r");
        json.ShouldEndWith("}\n");
        using var document = JsonDocument.Parse(json);
        var actions = document.RootElement.GetProperty("actions").EnumerateArray().ToList();
        actions.Select(action => action.GetProperty("code").GetString()).ShouldBe(catalogue.Select(entry => entry.Action.Code.Value));
        var read = actions.Single(action => action.GetProperty("code").GetString() == "platform.outbox.read");
        read.GetProperty("module").GetString().ShouldBe("Platform");
        read.GetProperty("scope").GetString().ShouldBe("PLATFORM");
        read.GetProperty("scopableBy").GetArrayLength().ShouldBe(0);
        read.GetProperty("audit").GetString().ShouldBe("REQUIRED");
        read.GetProperty("entitlement").ValueKind.ShouldBe(JsonValueKind.Null);
        read.GetProperty("idempotent").GetBoolean().ShouldBeFalse();
        read.GetProperty("allowsAnonymous").GetBoolean().ShouldBeFalse();
        read.GetProperty("availableWhenBlocked").GetBoolean().ShouldBeFalse();
        read.GetProperty("availableWhenReadOnly").GetBoolean().ShouldBeFalse();
        var access = actions.Single(action => action.GetProperty("code").GetString() == "tenant.access.read");
        access.GetProperty("availableWhenBlocked").GetBoolean().ShouldBeTrue();
        access.GetProperty("availableWhenReadOnly").GetBoolean().ShouldBeTrue();
        read.GetProperty("since").GetString().ShouldBe("S01");
    }

    [Fact]
    public void Tenant_action_fields_are_written_as_constants()
    {
        var action = new ActionDefinition(
            "fixture.reschedule", "Move a fixture | keep its round.", ActionScope.Tenant, AuditRequirement.RequiredWithReason, "S10",
            [AssignmentScope.Competition, AssignmentScope.Tenant], "fixtures.scheduling", idempotent: true);

        var markdown = ActionCatalogue.ToMarkdown([new ActionCatalogueEntry(action, "Fixtures")]);
        using var json = JsonDocument.Parse(ActionCatalogue.ToJson([new ActionCatalogueEntry(action, "Fixtures")]));

        markdown.ShouldContain("| `fixture.reschedule` | Fixtures | TENANT | TENANT, COMPETITION | REQUIRED_WITH_REASON | `fixtures.scheduling` | yes | no | no | no | S10 | Move a fixture \\| keep its round. |\n");
        json.RootElement.GetProperty("actions")[0].GetProperty("scopableBy").EnumerateArray()
            .Select(scope => scope.GetString()).ShouldBe(["TENANT", "COMPETITION"]);
    }

    // The exporter only finds actions declared as public readonly fields of static *Actions classes in module assemblies.
    [Fact]
    public void Every_static_action_is_a_public_readonly_field_of_an_Actions_class()
    {
        const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var catalogued = ModuleRegistry.All.Select(module => module.GetType().Assembly).ToHashSet();

        var misplaced = ScannedAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type =>
            {
                var declaring = catalogued.Contains(type.Assembly) && ActionCatalogue.DeclaringTypes(type.Assembly).Contains(type);
                var fields = type.GetFields(members)
                    .Where(field => field.FieldType == typeof(ActionDefinition) && !(declaring && field.IsInitOnly && field.IsPublic));
                var properties = type.GetProperties(members).Where(property => property.PropertyType == typeof(ActionDefinition));
                return fields.Select(field => $"{type.FullName}.{field.Name}")
                    .Concat(properties.Select(property => $"{type.FullName}.{property.Name}"));
            })
            .ToList();

        misplaced.ShouldBeEmpty();
    }

    [Fact]
    public void A_code_declared_twice_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() => ActionCatalogue.Build([.. ModuleRegistry.All, new DuplicateModule()]));

        exception.Message.ShouldContain("platform.outbox.read");
    }

    [Fact]
    public void Modules_sharing_an_assembly_are_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() => ActionCatalogue.Build([new DuplicateModule(), new SiblingModule()]));

        exception.Message.ShouldContain("Duplicate, Sibling");
    }

    private sealed class SiblingModule : IModule
    {
        public string Name => "Sibling";

        public string Schema => "sibling";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(ApiEndpointGroups groups)
        {
        }
    }

    private sealed class DuplicateModule : IModule
    {
        public string Name => "Duplicate";

        public string Schema => "duplicate";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(ApiEndpointGroups groups)
        {
        }
    }
}

/// <summary>Found by <see cref="ActionCatalogueTests.A_code_declared_twice_is_rejected"/> through the test assembly.</summary>
public static class DuplicateActions
{
    public static readonly ActionDefinition OutboxRead = new(
        "platform.outbox.read", "Declared twice on purpose.", ActionScope.Platform, AuditRequirement.None, "S01");
}
