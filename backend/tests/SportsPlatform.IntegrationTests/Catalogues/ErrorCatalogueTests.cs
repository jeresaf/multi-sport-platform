using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;

namespace SportsPlatform.IntegrationTests.Catalogues;

public sealed class ErrorCatalogueTests
{
    // docs/architecture/api-conventions.md, "Area prefixes".
    private static readonly string[] _areas =
    [
        "PLATFORM", "AUTH", "TENANT", "ENTITLEMENT", "SPORT", "FORMAT", "RULESET", "PERSON", "ORGANISATION", "TEAM",
        "VENUE", "MEMBERSHIP", "STAFF", "TRANSFER", "COMPETITION", "EDITION", "REGISTRATION", "ROSTER", "ELIGIBILITY",
        "STAGE", "FIXTURE", "CONTEST", "LINEUP", "OFFICIAL", "TIMELINE", "RESULT", "STANDINGS", "STATS", "SEARCH",
        "REPORT", "IMPORT", "PROVIDER", "NOTIFICATION", "BRAND", "SPONSOR", "MEDIA", "AUDIT", "INTEGRITY", "VALIDATION",
    ];

    // What the catalogue scans, plus the Contracts and the Host, where a declared error would be missed.
    private static IEnumerable<Assembly> ScannedAssemblies =>
    [
        typeof(Error).Assembly,
        typeof(ErrorCatalogue).Assembly,
        .. ModuleRegistry.All.Select(module => module.GetType().Assembly),
        .. ModuleRegistry.All.Select(module => Assembly.Load($"{module.GetType().Assembly.GetName().Name}.Contracts")),
    ];

    [Fact]
    public void Catalogue_has_the_pipeline_and_module_errors()
    {
        var catalogue = ErrorCatalogue.Build(ModuleRegistry.All);

        catalogue.ShouldContain(new ErrorCatalogueEntry(
            "PLATFORM_UNEXPECTED_ERROR", 500, CommonErrors.UnexpectedError.Message, ErrorCatalogue.BuildingBlocksOwner));
        catalogue.ShouldContain(entry => entry.Code == "PLATFORM_CONCURRENCY_CONFLICT" && entry.Status == 409 && entry.Owner == "Platform");
        catalogue.Select(entry => entry.Code).ShouldBe(catalogue.Select(entry => entry.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_code_uses_a_documented_area()
    {
        var undocumented = ErrorCatalogue.Build(ModuleRegistry.All)
            .Select(entry => entry.Code)
            .Where(code => !_areas.Contains(code[..code.IndexOf('_', StringComparison.Ordinal)]))
            .ToList();

        undocumented.ShouldBeEmpty();
    }

    // The exporter only finds errors declared as public readonly fields of static *Errors classes in BuildingBlocks
    // and module assemblies (not Contracts or the Host). Instance members
    // (Result.Error, a DbContext's ConcurrencyConflictError) refer to declared errors and aren't declarations.
    [Fact]
    public void Every_static_error_is_a_public_readonly_field_of_an_Errors_class()
    {
        const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var catalogued = new HashSet<Assembly>([typeof(Error).Assembly, .. ModuleRegistry.All.Select(module => module.GetType().Assembly)]);

        var misplaced = ScannedAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type =>
            {
                var declaring = catalogued.Contains(type.Assembly) && ErrorCatalogue.DeclaringTypes(type.Assembly).Contains(type);
                var fields = type.GetFields(members)
                    .Where(field => field.FieldType == typeof(Error) && !(declaring && field.IsInitOnly && field.IsPublic));
                var properties = type.GetProperties(members).Where(property => property.PropertyType == typeof(Error));
                return fields.Select(field => $"{type.FullName}.{field.Name}")
                    .Concat(properties.Select(property => $"{type.FullName}.{property.Name}"));
            })
            .ToList();

        misplaced.ShouldBeEmpty();
    }

    [Fact]
    public void A_code_declared_twice_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() => ErrorCatalogue.Build([.. ModuleRegistry.All, new DuplicateModule()]));

        exception.Message.ShouldContain("PLATFORM_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public void Markdown_escapes_pipes_in_messages()
    {
        var markdown = ErrorCatalogue.ToMarkdown([new ErrorCatalogueEntry("FIXTURE_VENUE_CLASH", 409, "Home | away clash.", "Fixtures")]);

        markdown.ShouldContain("| `FIXTURE_VENUE_CLASH` | 409 | Home \\| away clash. | Fixtures |\n");
        markdown.ShouldNotContain("\r");
    }

    [Fact]
    public void Json_lists_every_entry_with_LF_line_endings()
    {
        var catalogue = ErrorCatalogue.Build(ModuleRegistry.All);

        var json = ErrorCatalogue.ToJson(catalogue);

        json.ShouldNotContain("\r");
        json.ShouldEndWith("}\n");
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetProperty("code").GetString())
            .ShouldBe(catalogue.Select(entry => entry.Code));
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

/// <summary>Found by <see cref="ErrorCatalogueTests.A_code_declared_twice_is_rejected"/> through the test assembly.</summary>
public static class DuplicateErrors
{
    public static readonly Error ConcurrencyConflict = new("PLATFORM_CONCURRENCY_CONFLICT", "Declared twice on purpose.", ErrorKind.Conflict);
}
