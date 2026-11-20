using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.People.ChangePersonStatus;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Registry.Features.People.Consents;
using SportsPlatform.Modules.Registry.Features.People.CreatePerson;
using SportsPlatform.Modules.Registry.Features.People.Photo;
using SportsPlatform.Modules.Registry.Features.People.Profiles;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Registry.Seed.Demo;

/// <summary>
/// The demo tenants' people (S06 D23, docs/product/pilot-customer.md), from <c>Seed/Demo/Data/demo-people.json</c>
/// (data, not C#: it names sport variants, AR-11). Every change goes through the Registry's commands as the system
/// actor, one command per change: the duplicate check and create (with the file's reason where it expects candidates),
/// athlete, staff and official profiles, a guardian consent and its evidence, a photo, and archiving last (an archived
/// person takes no new profile, consent or photo). Photos and evidence are generated placeholder images, never photos
/// of anyone. Birth dates and consent dates are relative to the tenant's date when the person is created.
/// <para>
/// Idempotent by step: a person is found by name key (the earliest created, should the tenant have added another of
/// the name), then each step runs only when what it adds is missing (an athlete profile of the variant, any staff or
/// official profile, any consent, the first consent's evidence while it is active, a photo, the archived status). A
/// tenant that isn't <c>ACTIVE</c> waits; a missing one stops the run. After <c>tenancy.demo.sports</c> (6), whose
/// variants the profiles need.
/// </para>
/// </summary>
public sealed class DemoPeopleSeeder : ISeeder
{
    /// <summary>Bump when the seeder writes something different from the same data.</summary>
    private const int _version = 1;

    private const string _file = "demo-people.json";

    private const int _photoSize = 480;

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

    internal static IReadOnlyList<DemoTenantPeople> Tenants { get; } =
        JsonSerializer.Deserialize<DemoPeopleFile>(Read(), _options)?.Tenants
            ?? throw new InvalidOperationException($"Demo data file {_file} is empty.");

    public string Key => "registry.demo.people";

    public SeedCategory Category => SeedCategory.Demo;

    /// <summary>After the tenants (0), their plans (5) and sports (6) and the invitations (10).</summary>
    public int Order => 20;

    public string Checksum { get; } = $"v{_version}:{Convert.ToHexStringLower(SHA256.HashData(Read()))}";

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenants = context.Services.GetRequiredService<ITenantDirectory>();
        int inserted = 0, updated = 0;
        foreach (var demo in Tenants)
        {
            var found = await tenants.GetBySlugAsync(demo.Slug, cancellationToken);
            if (found.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Demo tenant '{demo.Slug}' doesn't exist: tenancy.demo.tenants runs first and creates it ({found.Error.Code}).");
            }

            if (found.Value.Status != TenantStatus.Active)
            {
                continue;
            }

            var tenantId = found.Value.Id;
            for (var index = 0; index < demo.People.Count; index++)
            {
                var person = demo.People[index];
                var state = await context.QueryAsTenantAsync(tenantId, (services, ct) => FindAsync(services, person, ct), cancellationToken);
                var created = state is null;
                if (state is null)
                {
                    await context.RunCommandAsync(
                        RegistryActions.PersonCreate, tenantId, (services, ct) => CreateAsync(services, person, ct), cancellationToken);
                    state = await context.QueryAsTenantAsync(tenantId, (services, ct) => FindAsync(services, person, ct), cancellationToken)
                        ?? throw new InvalidOperationException($"Demo person {person.Name} wasn't found after create.");
                }

                var changed = await CompleteAsync(context, tenantId, person, state, palette: index, cancellationToken);
                inserted += created ? 1 : 0;
                updated += !created && changed ? 1 : 0;
            }
        }

        return new SeedOutcome(inserted, updated, Deactivated: 0);
    }

    /// <summary>Runs each step the person still lacks; whether any ran.</summary>
    private static async Task<bool> CompleteAsync(
        SeedContext context, Guid tenantId, DemoPerson person, PersonState state, int palette, CancellationToken cancellationToken)
    {
        var id = state.Id;
        var changed = false;

        async Task RunAsync(ActionDefinition action, Func<IServiceProvider, CancellationToken, Task<FieldRefusal?>> command, string step)
        {
            await context.RunCommandAsync(
                action,
                tenantId,
                async (services, ct) =>
                {
                    if (await command(services, ct) is { } refusal)
                    {
                        throw new InvalidOperationException(
                            $"Seeding the {step} of demo person {person.Name} failed: {refusal.Error.Code} {refusal.Field} {refusal.Detail}");
                    }

                    return true;
                },
                cancellationToken);
            changed = true;
        }

        if (person.Athlete is { } athlete && !state.AthleteVariants.Contains(athlete.Variant))
        {
            var request = new CreateAthleteProfileRequest(athlete.Variant, athlete.PrimaryPosition, athlete.OtherPositions, athlete.DominantSide);
            await RunAsync(
                RegistryActions.AthleteProfileCreate,
                async (services, ct) =>
                {
                    await ValidateAsync(services, request, person, ct);
                    return await services.GetRequiredService<CreateAthleteProfileHandler>().HandleAsync(id, request, IfMatch.None, ct);
                },
                "athlete profile");
        }

        if (person.Staff is { Count: > 0 } staff && !state.HasStaffProfile)
        {
            var request = new StaffProfileRequest([.. staff.Select(s => new StaffCapabilityRequest(s.Variant, s.Role, s.Qualification))]);
            await RunAsync(
                RegistryActions.StaffProfileCreate,
                async (services, ct) =>
                {
                    await ValidateAsync(services, request, person, ct);
                    return await services.GetRequiredService<CreateStaffProfileHandler>().HandleAsync(id, request, IfMatch.None, ct);
                },
                "staff profile");
        }

        if (person.Official is { Count: > 0 } official && !state.HasOfficialProfile)
        {
            var request = new OfficialProfileRequest(
                [.. official.Select(o => new OfficialCapabilityRequest(o.Variant, o.Role, o.Grade, o.LicenceNumber, o.LicenceValidUntil))]);
            await RunAsync(
                RegistryActions.OfficialProfileCreate,
                async (services, ct) =>
                {
                    await ValidateAsync(services, request, person, ct);
                    return await services.GetRequiredService<CreateOfficialProfileHandler>().HandleAsync(id, request, IfMatch.None, ct);
                },
                "official profile");
        }

        var consentId = state.ConsentId;
        if (person.Consent is { } consent && consentId is null)
        {
            await RunAsync(
                RegistryActions.PersonConsentRecord,
                async (services, ct) =>
                {
                    var today = await services.GetRequiredService<TenantToday>().GetAsync(ct);
                    var request = new RecordConsentRequest(
                        consent.GuardianName, consent.Relationship, consent.Contact, consent.Scopes, today.AddDays(-consent.GivenDaysAgo));
                    await ValidateAsync(services, request, person, ct);
                    return await services.GetRequiredService<RecordConsentHandler>().HandleAsync(id, request, IfMatch.None, ct);
                },
                "consent");
            consentId = (await context.QueryAsTenantAsync(tenantId, (services, ct) => FindAsync(services, person, ct), cancellationToken))?.ConsentId;
        }

        if (person.Consent is { Evidence: true } && consentId is { } withEvidence && !(state.ConsentId == withEvidence && state.ConsentHasEvidence))
        {
            await RunAsync(
                RegistryActions.PersonConsentEvidenceUpload,
                async (services, ct) =>
                {
                    using var file = new MemoryStream(PlaceholderPng.Form());
                    return await services.GetRequiredService<UploadConsentEvidenceHandler>().HandleAsync(id, withEvidence, file, IfMatch.None, ct);
                },
                "consent evidence");
        }

        if (person.Photo && !state.HasPhoto)
        {
            await RunAsync(
                RegistryActions.PersonPhotoSet,
                async (services, ct) =>
                {
                    using var file = new MemoryStream(PlaceholderPng.Photo(_photoSize, palette));
                    return await services.GetRequiredService<SetPersonPhotoHandler>().HandleAsync(id, file, IfMatch.None, ct);
                },
                "photo");
        }

        if (person.ArchiveReason is { } reason && state.Status != PersonStatus.Archived)
        {
            await RunAsync(
                RegistryActions.PersonArchive,
                async (services, ct) =>
                {
                    await ValidateAsync(services, new ChangePersonStatusWithReasonRequest(reason), person, ct);
                    var archived = await services.GetRequiredService<ChangePersonStatusHandler>().HandleAsync(
                        id, PersonTransition.Archive, reason, version: null, IfMatch.None, ct);
                    return archived.IsFailure ? new FieldRefusal(archived.Error) : null;
                },
                "archive");
        }

        return changed;
    }

    /// <summary>
    /// The duplicate check, then create with its token, in one scope as the API's two requests would be. Candidates
    /// must match the file: none without a reason, at least one with one (the reason records the pair as decided).
    /// </summary>
    private static async Task<bool> CreateAsync(IServiceProvider services, DemoPerson person, CancellationToken cancellationToken)
    {
        var today = await services.GetRequiredService<TenantToday>().GetAsync(cancellationToken);
        var dateOfBirth = person.DateOfBirth(today);
        var check = new CheckDuplicatesRequest(
            person.FirstName, person.LastName, person.MiddleName, person.OtherNames, dateOfBirth, person.Nationality);
        await ValidateAsync(services, check, person, cancellationToken);
        var (answer, refused) = await services.GetRequiredService<CheckDuplicatesHandler>().HandleAsync(check, cancellationToken);
        if (answer is null)
        {
            throw new InvalidOperationException($"The duplicate check of demo person {person.Name} failed: {refused?.Error.Code} {refused?.Field}");
        }

        if (answer.Candidates.Count > 0 != person.OverrideReason is not null)
        {
            throw new InvalidOperationException(
                $"Demo person {person.Name} expected {(person.OverrideReason is null ? "no" : "a")} duplicate candidate and the check found " +
                $"{string.Join(", ", answer.Candidates.Select(c => $"{c.DisplayName} ({c.Confidence})"))}.");
        }

        var create = new CreatePersonRequest(
            person.FirstName,
            person.LastName,
            person.MiddleName,
            person.OtherNames,
            Gender: person.Gender,
            DateOfBirth: dateOfBirth,
            Nationality: person.Nationality,
            CheckToken: answer.CheckToken,
            OverrideReason: person.OverrideReason);
        await ValidateAsync(services, create, person, cancellationToken);
        var (personId, refusal) = await services.GetRequiredService<CreatePersonHandler>().HandleAsync(create, cancellationToken);
        if (personId is null)
        {
            throw new InvalidOperationException($"Creating demo person {person.Name} failed: {refusal?.Error.Code} {refusal?.Field} {refusal?.Detail}");
        }

        return true;
    }

    /// <summary>What the person already has, or null when the tenant has nobody of the name.</summary>
    private static async Task<PersonState?> FindAsync(IServiceProvider services, DemoPerson person, CancellationToken cancellationToken)
    {
        var context = services.GetRequiredService<RegistryDbContext>();
        var nameKey = NameNormaliser.NameKey(person.FirstName, person.MiddleName, person.LastName, person.OtherNames);

        // IDs are version 7: the earliest created comes first.
        var found = await context.Set<Person>().AsNoTracking()
            .Where(p => p.NameKey == nameKey)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Status, HasPhoto = p.PhotoAssetId != null })
            .FirstOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return null;
        }

        var variants = await context.Set<AthleteProfile>().AsNoTracking()
            .Where(a => a.PersonId == found.Id).Select(a => a.Variant).ToListAsync(cancellationToken);
        var hasStaff = await context.Set<StaffProfile>().AnyAsync(s => s.PersonId == found.Id, cancellationToken);
        var hasOfficial = await context.Set<OfficialProfile>().AnyAsync(o => o.PersonId == found.Id, cancellationToken);
        var consent = await context.Set<GuardianConsent>().AsNoTracking()
            .Where(c => c.PersonId == found.Id)
            .OrderBy(c => c.RecordedAt).ThenBy(c => c.Id)
            // A withdrawn consent takes no evidence: nothing is missing.
            .Select(c => new { c.Id, HasEvidence = c.EvidenceAssetId != null || c.Status != ConsentStatus.Active })
            .FirstOrDefaultAsync(cancellationToken);
        return new PersonState(
            found.Id, found.Status, found.HasPhoto, variants.ToHashSet(StringComparer.Ordinal), hasStaff, hasOfficial, consent?.Id, consent?.HasEvidence ?? false);
    }

    private static async Task ValidateAsync<T>(IServiceProvider services, T request, DemoPerson person, CancellationToken cancellationToken)
    {
        // As the endpoint does: handlers expect a validated request.
        var validation = await services.GetRequiredService<IValidator<T>>().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Demo data of {person.Name} is invalid for {typeof(T).Name}: {validation}");
        }
    }

    private static byte[] Read()
    {
        using var stream = typeof(DemoPeopleSeeder).Assembly.GetManifestResourceStream($"SportsPlatform.Modules.Registry.Seed.Demo.Data.{_file}")
            ?? throw new InvalidOperationException($"Demo data file {_file} is not embedded.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private sealed record PersonState(
        Guid Id,
        PersonStatus Status,
        bool HasPhoto,
        IReadOnlySet<string> AthleteVariants,
        bool HasStaffProfile,
        bool HasOfficialProfile,
        Guid? ConsentId,
        bool ConsentHasEvidence);
}

/// <summary>The shape of <c>demo-people.json</c>; <c>_comment</c> says what the file is.</summary>
internal sealed record DemoPeopleFile(
    [property: JsonPropertyName("_comment")] string Comment,
    IReadOnlyList<DemoTenantPeople> Tenants);

internal sealed record DemoTenantPeople(string Slug, IReadOnlyList<DemoPerson> People);

/// <summary>
/// A demo person. The birth date is <see cref="AgeYears"/> years and <see cref="AgeDays"/> days before the tenant's
/// date when they are created. <see cref="Club"/> is the team they play for or work with, for S08's teams (not seeded).
/// </summary>
internal sealed record DemoPerson(
    string FirstName,
    string LastName,
    Gender Gender,
    int AgeYears,
    int AgeDays,
    string Nationality,
    string? MiddleName = null,
    string? OtherNames = null,
    string? Club = null,
    string? OverrideReason = null,
    DemoAthlete? Athlete = null,
    IReadOnlyList<DemoStaffRole>? Staff = null,
    IReadOnlyList<DemoOfficialRole>? Official = null,
    DemoConsent? Consent = null,
    bool Photo = false,
    string? ArchiveReason = null)
{
    public string Name => string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(n => n is not null));

    public DateOnly DateOfBirth(DateOnly today) => today.AddYears(-AgeYears).AddDays(-AgeDays);
}

internal sealed record DemoAthlete(
    string Variant, string PrimaryPosition, IReadOnlyList<string>? OtherPositions = null, DominantSide? DominantSide = null);

internal sealed record DemoStaffRole(string Variant, string Role, string? Qualification = null);

internal sealed record DemoOfficialRole(
    string Variant, string Role, string? Grade = null, string? LicenceNumber = null, DateOnly? LicenceValidUntil = null);

/// <summary>A guardian consent given <see cref="GivenDaysAgo"/> days before the tenant's date; contacts are example.com.</summary>
internal sealed record DemoConsent(
    string GuardianName,
    GuardianRelationship Relationship,
    string Contact,
    IReadOnlyList<ConsentScope> Scopes,
    int GivenDaysAgo,
    bool Evidence = false);
