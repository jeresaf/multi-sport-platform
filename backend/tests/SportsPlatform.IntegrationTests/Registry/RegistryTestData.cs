using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Registry.Features.People.CreatePerson;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// People for the Registry tests, from docs/product/pilot-customer.md's kind of names (all invented). Saved through the
/// domain (<see cref="SavePersonAsync(IServiceProvider, Guid, PersonBiodata, CancellationToken)"/>) where a test needs
/// rows the commands can't make yet (aliases, archived people), else through the check and create handlers
/// (<see cref="CreatePersonAsync"/>, S06 D23).
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

    /// <summary>15 when the test clock starts (2026-10-17): a minor.</summary>
    public static PersonBiodata BrendaAkello { get; } =
        new("Brenda", "Akello", Gender: Gender.Female, DateOfBirth: new DateOnly(2011, 3, 2), Nationality: "UG");

    public static PersonBiodata DenisOpoka { get; } =
        new("Denis", "Opoka", MiddleName: "Okot", Gender: Gender.Male, DateOfBirth: new DateOnly(1990, 7, 5), Nationality: "UG",
            SecondaryNationality: "KE");

    /// <summary>No date of birth: age unknown.</summary>
    public static PersonBiodata IvanMukasa { get; } = new("Ivan", "Mukasa");

    public static PersonBiodata MariamNankya { get; } = new("Mariam", "Nankya", DateOfBirth: new DateOnly(2004, 9, 21));

    public static PersonBiodata ShamimNankya { get; } = new("Shamim", "Nankya", DateOfBirth: new DateOnly(2004, 9, 21));

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

    /// <summary>Saves a new person with an alias and an external identifier of a tenant's own authority.</summary>
    public static async Task<Person> SavePersonAsync(
        IServiceProvider services, Guid tenantId, PersonBiodata biodata, (string Name, AliasKind Kind) alias,
        (string Authority, string Value) identifier, CancellationToken cancellationToken)
    {
        await using var scope = ScopeFor(services, tenantId, out var context);
        var person = NewPerson(biodata);
        person.AddAlias(alias.Name, alias.Kind);
        context.Add(person);
        context.Add(ExternalIdentifier.ForPerson(person.Id, identifier.Authority, identifier.Value));
        await context.SaveChangesAsync(cancellationToken);
        return person;
    }

    /// <summary>
    /// Creates a person as <paramref name="actor"/> the way the API does: the duplicate check, then create with its token
    /// and <paramref name="overrideReason"/>. Throws when either refuses.
    /// </summary>
    public static async Task<Guid> CreatePersonAsync(
        IServiceProvider services, Guid tenantId, ICurrentActor actor, PersonBiodata biodata, string? overrideReason,
        CancellationToken cancellationToken)
    {
        await using var scope = ScopeFor(services, tenantId, out _);
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(actor);
        var (answer, refused) = await scope.ServiceProvider.GetRequiredService<CheckDuplicatesHandler>().HandleAsync(
            new CheckDuplicatesRequest(
                biodata.FirstName, biodata.LastName, biodata.MiddleName, biodata.OtherNames, biodata.DateOfBirth,
                biodata.Nationality, biodata.SecondaryNationality),
            cancellationToken);
        answer.ShouldNotBeNull(refused?.Error.Code);

        var (personId, refusal) = await scope.ServiceProvider.GetRequiredService<CreatePersonHandler>().HandleAsync(
            new CreatePersonRequest(
                biodata.FirstName, biodata.LastName, biodata.MiddleName, biodata.OtherNames, biodata.DisplayName,
                biodata.Gender, biodata.DateOfBirth, biodata.Nationality, biodata.SecondaryNationality, biodata.HeightCm,
                biodata.WeightKg, CheckToken: answer.CheckToken, OverrideReason: overrideReason),
            cancellationToken);
        refusal.ShouldBeNull(refusal?.Error.Code);
        return personId!.Value;
    }

    /// <summary>Moves a person along <paramref name="transition"/> as the system, with a reason (S06 D9).</summary>
    public static async Task ChangeStatusAsync(
        IServiceProvider services, Guid tenantId, Guid personId, PersonTransition transition, CancellationToken cancellationToken)
    {
        await using var scope = ScopeFor(services, tenantId, out var context);
        var person = await context.Set<Person>().SingleAsync(p => p.Id == personId, cancellationToken);
        person.ChangeStatus(transition, "Left the association", SystemActor.Instance, Now).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync(cancellationToken);
    }
}
