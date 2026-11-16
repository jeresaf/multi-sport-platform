using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// People for the Registry tests, from docs/product/pilot-customer.md's kind of names (all invented). Saved through the
/// domain until the create handler exists (S06-T2).
/// </summary>
internal static class RegistryTestData
{
    public static readonly Guid Kmsa = new("0192a1b2-0000-7000-8000-00000000c001");

    public static readonly Guid Elssc = new("0192a1b2-0000-7000-8000-00000000c002");

    public static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    public static PersonBiodata JohnSsekandi { get; } =
        new(
            "John", "Ssekandi", Gender: Gender.Male, DateOfBirth: new DateOnly(2001, 4, 12), Nationality: "UG",
            HeightCm: 178, WeightKg: 72.5m);

    public static Person NewPerson(PersonBiodata biodata) =>
        Person.Create(biodata, PersonSource.Manual, SystemActor.Instance, Now);

    /// <summary>Saves a new person under <paramref name="tenantId"/> in a scope of its own.</summary>
    public static async Task<Person> SavePersonAsync(
        IServiceProvider services, Guid tenantId, PersonBiodata biodata, CancellationToken cancellationToken)
    {
        await using var scope = ScopeFor(services, tenantId, out var context);
        var person = NewPerson(biodata);
        context.Add(person);
        await context.SaveChangesAsync(cancellationToken);
        return person;
    }

    public static AsyncServiceScope ScopeFor(IServiceProvider services, Guid tenantId, out RegistryDbContext context)
    {
        var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        context = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        return scope;
    }
}
