using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Plans;

namespace SportsPlatform.IntegrationTests.Catalogues;

public sealed class EntitlementCatalogueTests
{
    private static readonly IReadOnlyList<ActionCatalogueEntry> _actions = ActionCatalogue.Build(ModuleRegistry.All);

    [Fact]
    public void Catalogue_lists_every_Tenancy_entitlement()
    {
        var catalogue = EntitlementCatalogue.Build(ModuleRegistry.All);

        catalogue.Count.ShouldBe(17);
        catalogue.ShouldAllBe(entry => entry.Module == "Tenancy");
        catalogue.ShouldContain(new EntitlementCatalogueEntry(TenancyEntitlements.MaxStaffUsers, "Tenancy"));
        catalogue.Select(entry => entry.Entitlement.Code).ShouldBe(catalogue.Select(entry => entry.Entitlement.Code).Order(StringComparer.Ordinal));
    }

    // S04-T1 acceptance: every entitlement with its STANDARD and PRO values and the actions that name it.
    [Fact]
    public void Markdown_and_JSON_carry_each_plan_value_and_the_actions_naming_it()
    {
        var catalogue = EntitlementCatalogue.Build(ModuleRegistry.All);

        var markdown = EntitlementCatalogue.ToMarkdown(catalogue, TenancyPlans.All, _actions);
        var json = EntitlementCatalogue.ToJson(catalogue, TenancyPlans.All, _actions);

        markdown.ShouldContain("| Code | Name | Type | Unit | Levels | Default | STANDARD | PRO | Enforced by | Actions | Since | Description |\n");
        markdown.ShouldContain("| `MAX_STAFF_USERS` | Staff users | QUOTA | users | — | 0 | 100 | 1000 | Identity & Access | `membership.invite` | S04 |");
        markdown.ShouldContain("| `MAX_ENABLED_SPORTS` | Enabled sports | QUOTA | sports | — | 0 | 3 | unlimited | Tenancy & Commercial | — | S04 |");
        markdown.ShouldContain("| `REPORTS` | Reports | LEVEL | — | `BASIC` < `ADVANCED` | none | BASIC | ADVANCED | Insights | — | S04 |");
        markdown.ShouldContain("| `CUSTOM_DOMAIN` | Custom domain | BOOLEAN | — | — | no | no | yes | Tenancy & Commercial | — | S04 |");
        markdown.ShouldNotContain("\r");
        json.ShouldNotContain("\r");
        json.ShouldEndWith("}\n");

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("plans").EnumerateArray().Select(plan => plan.GetProperty("code").GetString()).ShouldBe(["STANDARD", "PRO"]);
        var entitlements = document.RootElement.GetProperty("entitlements").EnumerateArray().ToList();
        entitlements.Select(entitlement => entitlement.GetProperty("code").GetString()).ShouldBe(catalogue.Select(entry => entry.Entitlement.Code));

        var staff = entitlements.Single(entitlement => entitlement.GetProperty("code").GetString() == "MAX_STAFF_USERS");
        staff.GetProperty("module").GetString().ShouldBe("Tenancy");
        staff.GetProperty("type").GetString().ShouldBe("QUOTA");
        staff.GetProperty("unit").GetString().ShouldBe("users");
        staff.GetProperty("levels").GetArrayLength().ShouldBe(0);
        staff.GetProperty("default").GetProperty("limit").GetInt32().ShouldBe(0);
        staff.GetProperty("plans").GetProperty("STANDARD").GetProperty("limit").GetInt32().ShouldBe(100);
        staff.GetProperty("plans").GetProperty("PRO").GetProperty("limit").GetInt32().ShouldBe(1_000);
        staff.GetProperty("enforcedBy").GetString().ShouldBe("Identity & Access");
        staff.GetProperty("actions").EnumerateArray().Select(action => action.GetString()).ShouldBe(["membership.invite"]);
        staff.GetProperty("since").GetString().ShouldBe("S04");

        var sports = entitlements.Single(entitlement => entitlement.GetProperty("code").GetString() == "MAX_ENABLED_SPORTS");
        sports.GetProperty("plans").GetProperty("PRO").GetProperty("limit").ValueKind.ShouldBe(JsonValueKind.Null);

        var reports = entitlements.Single(entitlement => entitlement.GetProperty("code").GetString() == "REPORTS");
        reports.GetProperty("unit").ValueKind.ShouldBe(JsonValueKind.Null);
        reports.GetProperty("levels").EnumerateArray().Select(level => level.GetString()).ShouldBe(["BASIC", "ADVANCED"]);
        reports.GetProperty("default").GetProperty("level").ValueKind.ShouldBe(JsonValueKind.Null);
        reports.GetProperty("plans").GetProperty("PRO").GetProperty("level").GetString().ShouldBe("ADVANCED");

        var domain = entitlements.Single(entitlement => entitlement.GetProperty("code").GetString() == "CUSTOM_DOMAIN");
        domain.GetProperty("plans").GetProperty("STANDARD").GetProperty("enabled").GetBoolean().ShouldBeFalse();
        domain.GetProperty("plans").GetProperty("PRO").GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void A_plan_without_a_value_shows_a_dash_and_a_pipe_in_text_is_escaped()
    {
        var entitlement = new EntitlementDefinition(
            "API_ACCESS", "API | keys", "Tenant API keys | tokens.", EntitlementType.Boolean, "Identity & Access", "S04");
        var plan = new PlanDefinition("STANDARD", "Standard", 1, new Dictionary<string, EntitlementValue>());

        var markdown = EntitlementCatalogue.ToMarkdown([new EntitlementCatalogueEntry(entitlement, "Tenancy")], [plan], []);
        using var json = JsonDocument.Parse(EntitlementCatalogue.ToJson([new EntitlementCatalogueEntry(entitlement, "Tenancy")], [plan], []));

        markdown.ShouldContain("| `API_ACCESS` | API \\| keys | BOOLEAN | — | — | no | — | Identity & Access | — | S04 | Tenant API keys \\| tokens. |\n");
        json.RootElement.GetProperty("entitlements")[0].GetProperty("plans").GetProperty("STANDARD").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // The exporter only finds entitlements declared as public readonly fields of static *Entitlements classes.
    [Fact]
    public void Every_static_entitlement_is_a_public_readonly_field_of_an_Entitlements_class()
    {
        const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var catalogued = ModuleRegistry.All.Select(module => module.GetType().Assembly).ToHashSet();
        Assembly[] scanned =
        [
            typeof(EntitlementDefinition).Assembly,
            typeof(EntitlementCatalogue).Assembly,
            .. catalogued,
            .. ModuleRegistry.All.Select(module => Assembly.Load($"{module.GetType().Assembly.GetName().Name}.Contracts")),
        ];

        var misplaced = scanned
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type =>
            {
                var declaring = catalogued.Contains(type.Assembly) && EntitlementCatalogue.DeclaringTypes(type.Assembly).Contains(type);
                var fields = type.GetFields(members)
                    .Where(field => field.FieldType == typeof(EntitlementDefinition) && !(declaring && field.IsInitOnly && field.IsPublic));
                var properties = type.GetProperties(members).Where(property => property.PropertyType == typeof(EntitlementDefinition));
                return fields.Select(field => $"{type.FullName}.{field.Name}")
                    .Concat(properties.Select(property => $"{type.FullName}.{property.Name}"));
            })
            .ToList();

        misplaced.ShouldBeEmpty();
    }

    [Fact]
    public void A_code_declared_twice_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() => EntitlementCatalogue.Build([.. ModuleRegistry.All, new DuplicateModule()]));

        exception.Message.ShouldContain("MAX_TEAMS");
    }

    [Fact]
    public void The_service_finds_entitlements_by_code()
    {
        var catalogue = new DeclaredEntitlements(ModuleRegistry.All);

        catalogue.Find("MAX_STAFF_USERS").ShouldBeSameAs(TenancyEntitlements.MaxStaffUsers);
        catalogue.Find("max_staff_users").ShouldBeNull();
        catalogue.All.Count.ShouldBe(17);
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

/// <summary>Found by <see cref="EntitlementCatalogueTests.A_code_declared_twice_is_rejected"/> through the test assembly.</summary>
public static class DuplicateEntitlements
{
    public static readonly EntitlementDefinition MaxTeams = new(
        "MAX_TEAMS", "Teams", "Declared twice on purpose.", EntitlementType.Quota, "Registry", "S04", unit: "teams");
}
