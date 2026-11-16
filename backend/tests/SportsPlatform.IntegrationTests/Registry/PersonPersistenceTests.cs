using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;

namespace SportsPlatform.IntegrationTests.Registry;

// S06-T1a: the person's tables, the name key's trigram index and the aggregate's version (D2–D4, D10, D11).
public sealed class PersonPersistenceTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task A_person_is_saved_with_its_keys_its_history_and_its_tenant()
    {
        var saved = await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, RegistryTestData.JohnSsekandi, Ct);

        await using var scope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var context);
        var person = await context.Set<Person>().Include(p => p.StatusHistory).Include(p => p.Aliases).SingleAsync(p => p.Id == saved.Id, Ct);

        person.TenantId.ShouldBe(RegistryTestData.Kmsa);
        person.DisplayName.ShouldBe("John Ssekandi");
        person.NameKey.ShouldBe("john ssekandi");
        person.Gender.ShouldBe(Gender.Male);
        person.DateOfBirth.ShouldBe(new DateOnly(2001, 4, 12));
        person.Nationality.ShouldBe("UG");
        person.HeightCm.ShouldBe(178);
        person.WeightKg.ShouldBe(72.5m);
        person.Status.ShouldBe(PersonStatus.Active);
        person.Aliases.ShouldBeEmpty();
        person.StatusHistory.ShouldHaveSingleItem().TenantId.ShouldBe(RegistryTestData.Kmsa);
    }

    [Fact]
    public async Task Another_tenant_does_not_see_the_person()
    {
        var saved = await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, RegistryTestData.JohnSsekandi, Ct);

        await using var scope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Elssc, out var context);

        (await context.Set<Person>().AnyAsync(p => p.Id == saved.Id, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Two_concurrent_changes_to_different_aliases_of_one_person_conflict()
    {
        var saved = await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, new PersonBiodata("Peter", "Ochieng"), Ct);

        await using (var firstScope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var first))
        await using (var secondScope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var second))
        {
            var firstCopy = await first.Set<Person>().SingleAsync(p => p.Id == saved.Id, Ct);
            var secondCopy = await second.Set<Person>().SingleAsync(p => p.Id == saved.Id, Ct);

            secondCopy.AddAlias("Peter Okot", AliasKind.FormerName);
            (await second.SaveChangesResultAsync(Ct)).IsSuccess.ShouldBeTrue();

            firstCopy.AddAlias("Pito", AliasKind.Nickname);
            var result = await first.SaveChangesResultAsync(Ct);

            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("PERSON_CONCURRENCY_CONFLICT");
        }

        await using var checkScope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var check);
        (await check.Set<PersonAlias>().Where(a => a.PersonId == saved.Id).Select(a => a.Name).ToListAsync(Ct)).ShouldBe(["Peter Okot"]);
    }

    [Fact]
    public async Task Two_concurrent_changes_to_a_persons_identifiers_conflict_when_the_person_is_marked_changed()
    {
        var saved = await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, new PersonBiodata("Mariam", "Nankya"), Ct);

        await using var firstScope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var first);
        await using var secondScope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var second);
        var firstCopy = await first.Set<Person>().SingleAsync(p => p.Id == saved.Id, Ct);
        var secondCopy = await second.Set<Person>().SingleAsync(p => p.Id == saved.Id, Ct);

        second.Add(ExternalIdentifier.ForPerson(secondCopy.Id, "KMSA_REG_NO", "KM-2026-0147"));
        second.MarkChanged(secondCopy);
        (await second.SaveChangesResultAsync(Ct)).IsSuccess.ShouldBeTrue();

        first.Add(ExternalIdentifier.ForPerson(firstCopy.Id, "SCHOOL_ADMISSION_NO", "NHS/0932"));
        first.MarkChanged(firstCopy);
        var result = await first.SaveChangesResultAsync(Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PERSON_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task An_identifier_value_is_unique_per_tenant_authority_and_entity_type()
    {
        var mariam = await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, new PersonBiodata("Mariam", "Nankya"), Ct);
        var shamim = await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, new PersonBiodata("Shamim", "Nankya"), Ct);

        await using (var scope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var context))
        {
            context.Add(ExternalIdentifier.ForPerson(mariam.Id, "KMSA_REG_NO", "KM-2026-0147"));
            await context.SaveChangesAsync(Ct);
        }

        await using (var scope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var context))
        {
            context.Add(ExternalIdentifier.ForPerson(shamim.Id, "KMSA_REG_NO", "KM-2026-0147"));
            var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
            exception.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("ux_external_identifiers_tenant_id_authority_entity_type_value");
        }

        // Another tenant may hold the same value.
        await using (var scope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Elssc, out var context))
        {
            context.Add(ExternalIdentifier.ForPerson(Guid.CreateVersion7(), "KMSA_REG_NO", "KM-2026-0147"));
            await context.SaveChangesAsync(Ct);
        }
    }

    [Fact]
    public async Task The_status_history_cannot_be_changed()
    {
        var saved = await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, RegistryTestData.JohnSsekandi, Ct);

        await using var scope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var context);
        var exception = await Should.ThrowAsync<PostgresException>(() => context.Database.ExecuteSqlAsync(
            $"UPDATE registry.person_status_changes SET reason = 'Entered twice' WHERE person_id = {saved.Id}", Ct));

        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Name_keys_are_compared_by_trigram_similarity_in_the_database()
    {
        await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, RegistryTestData.JohnSsekandi, Ct);
        await RegistryTestData.SavePersonAsync(App.Services, RegistryTestData.Kmsa, new PersonBiodata("Brenda", "Akello"), Ct);
        var key = NameNormaliser.NameKey("Jon Sekandi");

        await using var scope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var context);
        var matches = await context.Set<Person>()
            .Where(p => EF.Functions.TrigramsSimilarity(p.NameKey, key) >= 0.5)
            .Select(p => p.DisplayName)
            .ToListAsync(Ct);

        matches.ShouldBe(["John Ssekandi"]);
    }

    [Fact]
    public async Task The_name_keys_have_trigram_indexes()
    {
        var indexes = await QueryAsync(
            DatabaseRoles.Migrator,
            "SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'registry' AND indexname LIKE '%name_key_trgm'",
            reader => (Name: reader.GetString(0), Definition: reader.GetString(1)));

        indexes.Select(index => index.Name).Order().ShouldBe(["ix_person_aliases_name_key_trgm", "ix_persons_name_key_trgm"]);
        indexes.ShouldAllBe(index => index.Definition.Contains("USING gin (name_key gin_trgm_ops)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_trigram_extension_is_in_schema_public()
    {
        var schemas = await QueryAsync(
            DatabaseRoles.Migrator,
            "SELECT n.nspname FROM pg_extension e JOIN pg_namespace n ON n.oid = e.extnamespace WHERE e.extname = 'pg_trgm'",
            reader => reader.GetString(0));

        schemas.ShouldBe(["public"]);
    }

    [Fact]
    public void Every_personal_column_is_classified_and_the_date_of_birth_is_private()
    {
        using var scope = RegistryTestData.ScopeFor(App.Services, RegistryTestData.Kmsa, out var context);
        var person = context.Model.FindEntityType(typeof(Person))!;
        string[] unclassified = [
            nameof(Person.Id), nameof(Person.TenantId), nameof(Person.Source), nameof(Person.ImportJobId),
            AuditColumns.CreatedAt, AuditColumns.CreatedBy, AuditColumns.UpdatedAt, AuditColumns.UpdatedBy, AuditColumns.Version,
        ];

        var missing = person.GetProperties()
            .Where(property => property.FindAnnotation(Pii.Annotation) is null)
            .Select(property => property.Name)
            .Except(unclassified, StringComparer.Ordinal)
            .ToList();

        missing.ShouldBeEmpty();
        person.FindProperty(nameof(Person.DateOfBirth))![Pii.Annotation].ShouldBe("PRIVATE");
        person.FindProperty(nameof(Person.DisplayName))![Pii.Annotation].ShouldBe("PUBLIC");
        person.FindProperty(nameof(Person.FirstName))![Pii.Annotation].ShouldBe("INTERNAL");
    }
}
