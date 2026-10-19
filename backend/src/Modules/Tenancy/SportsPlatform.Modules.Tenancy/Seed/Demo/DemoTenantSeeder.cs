using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Settings.UpdateTenantSettings;
using SportsPlatform.Modules.Tenancy.Features.Tenants.ChangeTenantStatus;
using SportsPlatform.Modules.Tenancy.Features.Tenants.CreateTenant;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Seed.Demo;

/// <summary>
/// The pilot customer's tenants (docs/product/pilot-customer.md): KMSA and ELSSC, <c>ACTIVE</c>, public, with guardian
/// consent required before minors register. Through the Tenancy commands, each audited with source <c>SYSTEM</c>, and
/// idempotent by slug: a missing tenant is created with its deterministic ID (<c>tenancy.tenant:&lt;slug&gt;</c>), a
/// <c>PROVISIONING</c> one is activated, and settings that differ from the data are changed. A tenant in any other
/// status keeps it, and a profile changed since is left alone.
/// </summary>
public sealed class DemoTenantSeeder : ISeeder
{
    /// <summary>Bump when the seeder writes something different from the same data.</summary>
    private const int _version = 1;

    internal static readonly IReadOnlyList<DemoTenant> Tenants =
    [
        new("kmsa", "Kampala Metro Sports Association"),
        new("elssc", "Eastern Lakes Schools Sports Council"),
    ];

    public string Key => "tenancy.demo.tenants";

    public SeedCategory Category => SeedCategory.Demo;

    public int Order => 0;

    public string Checksum { get; } = ComputeChecksum();

    /// <summary>The ID every environment gives the demo tenant <paramref name="slug"/>.</summary>
    public static Guid TenantId(string slug) => DeterministicIds.Create($"tenancy.tenant:{slug}");

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        int inserted = 0, updated = 0;
        foreach (var demo in Tenants)
        {
            var id = TenantId(demo.Slug);
            var status = await FindStatusAsync(context, demo, id, cancellationToken);
            var created = status is null;
            if (created)
            {
                // No tenant yet: the handler checks the slug first and sets the new tenant before its save (S02 D1, #30 R2).
                await context.RunCommandAsync(TenancyActions.TenantCreate, tenantId: null, (services, ct) => CreateAsync(services, demo, id, ct), cancellationToken);
                status = TenantStatus.Provisioning;
            }

            var changed = false;
            if (status == TenantStatus.Provisioning)
            {
                await context.RunCommandAsync(TenancyActions.TenantActivate, id, (services, ct) => ActivateAsync(services, id, ct), cancellationToken);
                changed = true;
            }

            var settingsChange = await context.QueryAsTenantAsync(id, SettingsChangeAsync, cancellationToken);
            if (settingsChange is not null)
            {
                await context.RunCommandAsync(
                    TenancyActions.TenantSettingsUpdate, id, (services, ct) => UpdateSettingsAsync(services, settingsChange, ct), cancellationToken);
                changed = true;
            }

            inserted += created ? 1 : 0;
            updated += !created && changed ? 1 : 0;
        }

        return new SeedOutcome(inserted, updated, Deactivated: 0);
    }

    private static async Task<TenantStatus?> FindStatusAsync(
        SeedContext context, DemoTenant demo, Guid id, CancellationToken cancellationToken)
    {
        // The tenants table is global: no tenant is needed to read it.
        var db = context.Services.GetRequiredService<TenancyDbContext>();
        var tenant = await db.Set<Tenant>().AsNoTracking()
            .Where(t => t.Slug == demo.Slug)
            .Select(t => new { t.Id, t.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        if (tenant.Id != id)
        {
            throw new InvalidOperationException(
                $"Tenant '{demo.Slug}' exists with ID {tenant.Id}, not its seed ID {id}: it wasn't created by this seeder.");
        }

        return tenant.Status;
    }

    private static async Task<bool> CreateAsync(IServiceProvider services, DemoTenant demo, Guid id, CancellationToken cancellationToken)
    {
        var request = new CreateTenantRequest(
            demo.Slug, demo.Name, demo.Name, DemoTenant.CountryCode, DemoTenant.TimeZone, DemoTenant.DefaultLocale,
            DemoTenant.CurrencyCode, DemoTenant.PublicEnabled);
        await ValidateAsync(services, request, cancellationToken);
        Succeed(await services.GetRequiredService<CreateTenantHandler>().HandleAsync(request, id, cancellationToken), demo.Slug);
        return true;
    }

    private static async Task<bool> ActivateAsync(IServiceProvider services, Guid id, CancellationToken cancellationToken)
    {
        var result = await services.GetRequiredService<ChangeTenantStatusHandler>().HandleAsync(
            id, TenantTransition.Activate, reason: null, version: null, IfMatch.None, cancellationToken);
        Succeed(result, id.ToString());
        return true;
    }

    /// <summary>The settings keys that differ from the data, or null when none do.</summary>
    private static async Task<UpdateTenantSettingsRequest?> SettingsChangeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var current = await services.GetRequiredService<TenancyDbContext>().Set<TenantSettings>().AsNoTracking().SingleAsync(cancellationToken);
        var request = new UpdateTenantSettingsRequest(
            current.TimeZone == DemoTenant.TimeZone ? null : DemoTenant.TimeZone,
            current.DefaultLocale == DemoTenant.DefaultLocale ? null : DemoTenant.DefaultLocale,
            current.CurrencyCode == DemoTenant.CurrencyCode ? null : DemoTenant.CurrencyCode,
            current.PublicEnabled == DemoTenant.PublicEnabled ? null : DemoTenant.PublicEnabled,
            StartingSoonMinutes: null,
            current.MinorRegistrationConsent == DemoTenant.Consent ? null : DemoTenant.Consent,
            Version: null);
        return request == new UpdateTenantSettingsRequest(null, null, null, null, null, null, null) ? null : request;
    }

    private static async Task<bool> UpdateSettingsAsync(
        IServiceProvider services, UpdateTenantSettingsRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(services, request, cancellationToken);
        Succeed(await services.GetRequiredService<UpdateTenantSettingsHandler>().HandleAsync(request, IfMatch.None, cancellationToken), "settings");
        return true;
    }

    private static async Task ValidateAsync<T>(IServiceProvider services, T request, CancellationToken cancellationToken)
    {
        // As the endpoint does: handlers expect a validated request.
        var validation = await services.GetRequiredService<IValidator<T>>().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Demo tenant data is invalid: {validation}");
        }
    }

    private static void Succeed<T>(Result<T> result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seeding demo tenant {what} failed: {result.Error.Code} {result.Error.Message}");
        }
    }

    private static string ComputeChecksum()
    {
        var data = JsonSerializer.Serialize(new
        {
            version = _version,
            tenants = Tenants,
            DemoTenant.CountryCode,
            DemoTenant.TimeZone,
            DemoTenant.DefaultLocale,
            DemoTenant.CurrencyCode,
            DemoTenant.PublicEnabled,
            consent = DemoTenant.Consent.ToString(),
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}

/// <summary>A demo tenant; legal and display name are the same. Every demo tenant shares the settings below.</summary>
internal sealed record DemoTenant(string Slug, string Name)
{
    public const string CountryCode = "UG";

    public const string TimeZone = "Africa/Kampala";

    public const string DefaultLocale = "en-UG";

    public const string CurrencyCode = "UGX";

    public const bool PublicEnabled = true;

    public const MinorRegistrationConsent Consent = MinorRegistrationConsent.Required;
}
