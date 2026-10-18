using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Host;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary>AR-21: DTOs are not entities. No endpoint documents a response that is, or contains, an entity.</summary>
public sealed class DtoRulesTests
{
    [Fact]
    public async Task No_endpoint_response_type_reaches_an_entity()
    {
        // Built, never started: no server, no startup migration.
        await using var app = SportsPlatformApp.Build(WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = TestEnvironment.Name }));

        var leaks = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Where(produces => produces.Type is not null)
                .SelectMany(produces => EntitiesReachableFrom(produces.Type!))
                .Select(entity => $"{endpoint.DisplayName} → {entity.FullName}"))
            .Distinct()
            .ToList();

        leaks.ShouldBeEmpty();
    }

    [Fact]
    public void Entities_are_found_through_generics_collections_and_properties()
    {
        EntitiesReachableFrom(typeof(List<Wrapper>)).ShouldBe([typeof(Leaky)]);
        EntitiesReachableFrom(typeof(Wrapper[])).ShouldBe([typeof(Leaky)]);
        EntitiesReachableFrom(typeof(Leaky)).ShouldBe([typeof(Leaky)]);
        EntitiesReachableFrom(typeof(Safe)).ShouldBeEmpty();
    }

    /// <summary>
    /// The type, its generic arguments and element types, and (for solution types) its public instance properties: what
    /// is serialized. Static members (e.g. a query projection from the entity) are not.
    /// </summary>
    private static List<Type> EntitiesReachableFrom(Type root)
    {
        var seen = new HashSet<Type>();
        var entities = new List<Type>();
        var pending = new Stack<Type>([root]);
        while (pending.TryPop(out var type))
        {
            if (!seen.Add(type))
            {
                continue;
            }

            if (typeof(Entity).IsAssignableFrom(type))
            {
                entities.Add(type);
                continue;
            }

            foreach (var related in type.GetGenericArguments().Append(type.GetElementType()).OfType<Type>())
            {
                pending.Push(related);
            }

            if (type.Namespace?.StartsWith("SportsPlatform.", StringComparison.Ordinal) == true)
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    pending.Push(property.PropertyType);
                }
            }
        }

        return entities;
    }

    private sealed class Leaky : Entity
    {
        public Leaky()
            : base(Guid.CreateVersion7())
        {
        }
    }

    private sealed record Wrapper(string Name, Leaky Entity);

    private sealed record Safe(string Name, IReadOnlyList<Guid> Ids)
    {
        public static Expression<Func<Leaky, Safe>> Projection { get; } = l => new Safe("Kololo", new[] { l.Id });
    }
}
