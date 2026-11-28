using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Registry.Seed.Demo;

/// <summary>
/// What the S07 DEMO seeders (venues, organisations, teams; S07 D22) share: the embedded JSON files, the active demo
/// tenant, request validation as the endpoint does it, and the message that stops a run when a command refuses.
/// </summary>
internal static class DemoRegistryData
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

    /// <summary>The file's contents, read when the seeder is built so a malformed file fails every run.</summary>
    public static T Load<T>(string file) =>
        JsonSerializer.Deserialize<T>(Read(file), _options) ?? throw new InvalidOperationException($"Demo data file {file} is empty.");

    /// <summary>The seeder's checksum: its version and the file's hash, so a change to either runs it again.</summary>
    public static string Checksum(int version, string file) => $"v{version}:{Convert.ToHexStringLower(SHA256.HashData(Read(file)))}";

    /// <summary>
    /// The demo tenant's ID while it is <c>ACTIVE</c>; null otherwise (skipped until the seeder next runs, as
    /// <c>registry.demo.people</c>). A missing tenant stops the run: <c>tenancy.demo.tenants</c> runs first.
    /// </summary>
    public static async Task<Guid?> ActiveTenantAsync(SeedContext context, string slug, CancellationToken cancellationToken)
    {
        var found = await context.Services.GetRequiredService<ITenantDirectory>().GetBySlugAsync(slug, cancellationToken);
        if (found.IsFailure)
        {
            throw new InvalidOperationException(
                $"Demo tenant '{slug}' doesn't exist: tenancy.demo.tenants runs first and creates it ({found.Error.Code}).");
        }

        return found.Value.Status == TenantStatus.Active ? found.Value.Id : null;
    }

    /// <summary>Validates the request as the endpoint would (handlers expect a validated request).</summary>
    public static async Task ValidateAsync<T>(IServiceProvider services, T request, string what, CancellationToken cancellationToken)
    {
        var validation = await services.GetRequiredService<IValidator<T>>().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join(", ", validation.Errors.Select(e => $"{e.PropertyName} {e.ErrorCode}"));
            throw new InvalidOperationException($"Demo data of {what} is invalid for {typeof(T).Name}: {errors}");
        }
    }

    /// <summary>Stops the run when a command refused: its code, fields and, for a duplicate warning, the candidates.</summary>
    public static void ThrowIfRefused(ProblemHttpResult? refusal, string step)
    {
        if (refusal is not null)
        {
            // Registry data only (names of venues, organisations and teams), no personal data.
            var extensions = refusal.ProblemDetails.Extensions.Where(e => e.Key != "correlationId").ToDictionary();
            throw new InvalidOperationException($"Seeding {step} failed: {JsonSerializer.Serialize(extensions)}");
        }
    }

    /// <summary><see cref="ThrowIfRefused(ProblemHttpResult?, string)"/> for the handlers that answer a field refusal.</summary>
    public static void ThrowIfRefused(FieldRefusal? refusal, string step)
    {
        if (refusal is not null)
        {
            throw new InvalidOperationException($"Seeding {step} failed: {refusal.Error.Code} {refusal.Field}");
        }
    }

    private static byte[] Read(string file)
    {
        using var stream = typeof(DemoRegistryData).Assembly.GetManifestResourceStream($"SportsPlatform.Modules.Registry.Seed.Demo.Data.{file}")
            ?? throw new InvalidOperationException($"Demo data file {file} is not embedded.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
