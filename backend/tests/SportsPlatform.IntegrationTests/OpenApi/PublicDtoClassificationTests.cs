using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Host;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Features.People.ListPersonStatusChanges;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary>
/// AR-21, S06 D18: public responses contain only <c>PUBLIC</c> fields. Every property of every response type of a
/// public endpoint (<c>.AllowPublic()</c>), nested types included, is declared <c>[PersonalData(Public)]</c>: a
/// property nobody classified fails, so a field can't reach the public API by being forgotten. And S06 D3 for the
/// Registry's admin reads: <c>person.read</c> answers no <c>PRIVATE</c> field.
/// </summary>
public sealed class PublicDtoClassificationTests
{
    [Fact]
    public async Task Every_property_of_every_public_response_is_classified_public()
    {
        // Built, never started: no server, no startup migration.
        await using var app = SportsPlatformApp.Build(WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = TestEnvironment.Name }));

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AllowPublicMetadata>() is not null)
            .ToList();
        var violations = endpoints
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Where(produces => produces.Type is not null)
                .SelectMany(produces => NotClassified(produces.Type!, PersonalDataClass.Public))
                .Select(property => $"{endpoint.DisplayName} → {property}"))
            .Distinct()
            .ToList();

        // A public API endpoint that documents no typed response would have nothing to check: it must document one.
        var untyped = endpoints
            .Where(endpoint => endpoint is RouteEndpoint route
                && route.RoutePattern.RawText!.StartsWith("/api/public/", StringComparison.Ordinal)
                && !endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
                    .Any(produces => produces.StatusCode is >= 200 and < 300 && produces.Type is not null && produces.Type != typeof(void)))
            .Select(endpoint => endpoint.DisplayName)
            .ToList();

        endpoints.ShouldContain(endpoint => endpoint.DisplayName!.Contains("/api/public/v1/site", StringComparison.Ordinal));
        untyped.ShouldBeEmpty();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Unclassified_and_non_public_properties_are_found_through_collections_and_nested_types()
    {
        NotClassified(typeof(List<Outer>), PersonalDataClass.Public).ShouldBe(
        [
            $"{typeof(Outer).FullName}.Unclassified (not classified)",
            $"{typeof(Inner).FullName}.LegalName (INTERNAL)",
            $"{typeof(Inner).FullName}.DateOfBirth (PRIVATE)",
        ], ignoreOrder: true);
        NotClassified(typeof(Loose), PersonalDataClass.Public).ShouldBe(
        [
            $"{typeof(Loose).FullName}.Anything (untyped: object)",
            $"{typeof(Loose).FullName}.Raw (untyped: JsonElement)",
            $"{typeof(Loose).FullName}.Bag (untyped: object)",
        ], ignoreOrder: true);
        NotClassified(typeof(Inner[]), PersonalDataClass.Private).ShouldBeEmpty();
    }

    // T1 acceptance: person.read answers no PRIVATE field. Every property of the responses of every endpoint that
    // declares person.read is classified, at most INTERNAL; the page envelope (BuildingBlocks) holds no personal value.
    [Fact]
    public async Task The_person_read_answers_classified_fields_that_are_at_most_internal()
    {
        await using var app = SportsPlatformApp.Build(WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = TestEnvironment.Name }));

        var responses = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequiresActionMetadata>()?.Action == RegistryActions.PersonRead)
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
            .Where(produces => produces.StatusCode is >= 200 and < 300 && produces.Type is not null)
            .Select(produces => produces.Type!)
            .Distinct()
            .ToList();

        responses.ShouldBe([typeof(ListPeopleResponse), typeof(PersonResponse), typeof(ListPersonStatusChangesResponse)], ignoreOrder: true);
        responses.SelectMany(response => NotClassified(response, PersonalDataClass.Internal, InRegistry)).ShouldBeEmpty();
    }

    // T6a: the guardian and the evidence of a consent are PRIVATE too, and only the private read answers them.
    [Fact]
    public void The_private_read_is_the_one_that_answers_a_private_field() =>
        NotClassified(typeof(PersonPrivateResponse), PersonalDataClass.Internal, InRegistry).ShouldBe(
        [
            $"{typeof(PersonPrivateResponse).FullName}.DateOfBirth (PRIVATE)",
            $"{typeof(ConsentPrivateResponse).FullName}.GuardianName (PRIVATE)",
            $"{typeof(ConsentPrivateResponse).FullName}.Relationship (PRIVATE)",
            $"{typeof(ConsentPrivateResponse).FullName}.Contact (PRIVATE)",
            $"{typeof(ConsentPrivateResponse).FullName}.Evidence (PRIVATE)",
            $"{typeof(ConsentEvidenceResponse).FullName}.Url (PRIVATE)",
            $"{typeof(ConsentEvidenceResponse).FullName}.ExpiresAt (PRIVATE)",
        ], ignoreOrder: true);

    private static bool InRegistry(Type type) =>
        type.Namespace!.StartsWith("SportsPlatform.Modules.Registry.", StringComparison.Ordinal);

    /// <summary>
    /// The serialized properties reachable from <paramref name="root"/> (through generic arguments, element types and
    /// the properties of solution types) that carry no classification or one above <paramref name="highest"/>. Only
    /// the types <paramref name="mustClassify"/> accepts (every solution type without it) are checked.
    /// </summary>
    private static List<string> NotClassified(Type root, PersonalDataClass highest, Func<Type, bool>? mustClassify = null)
    {
        var seen = new HashSet<Type>();
        var found = new List<string>();
        var pending = new Stack<Type>([root]);
        while (pending.TryPop(out var type))
        {
            if (!seen.Add(type))
            {
                continue;
            }

            foreach (var related in type.GetGenericArguments().Append(type.GetElementType()).OfType<Type>())
            {
                pending.Push(related);
            }

            if (type.IsEnum || type.IsArray || type.Namespace?.StartsWith("SportsPlatform.", StringComparison.Ordinal) != true)
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                pending.Push(property.PropertyType);
                if (mustClassify?.Invoke(type) == false)
                {
                    continue;
                }

                // A value of any shape can't be checked: such a property has no place in a classified response.
                var untyped = new[] { property.PropertyType }.Concat(property.PropertyType.GetGenericArguments())
                    .FirstOrDefault(part => part == typeof(object) || part == typeof(JsonElement) || part == typeof(JsonDocument));
                var classification = property.GetCustomAttribute<PersonalDataAttribute>()?.Classification;
                if (untyped is not null)
                {
                    found.Add($"{type.FullName}.{property.Name} (untyped: {(untyped == typeof(object) ? "object" : untyped.Name)})");
                }
                else if (classification is null)
                {
                    found.Add($"{type.FullName}.{property.Name} (not classified)");
                }
                else if (classification > highest)
                {
                    found.Add($"{type.FullName}.{property.Name} ({classification.Value.ToString().ToUpperInvariant()})");
                }
            }
        }

        return found;
    }

    private sealed record Outer(
        [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
        string Unclassified,
        [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<Inner> People);

    private sealed record Loose(
        [property: PersonalData(PersonalDataClass.Public)] object Anything,
        [property: PersonalData(PersonalDataClass.Public)] JsonElement Raw,
        [property: PersonalData(PersonalDataClass.Public)] IReadOnlyDictionary<string, object> Bag);

    private sealed record Inner(
        [property: PersonalData(PersonalDataClass.Internal)] string LegalName,
        [property: PersonalData(PersonalDataClass.Private)] DateOnly DateOfBirth);
}
