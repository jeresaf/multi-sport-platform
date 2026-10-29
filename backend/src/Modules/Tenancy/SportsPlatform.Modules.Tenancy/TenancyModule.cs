using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.Modules.Tenancy.Features.Access.GetTenantAccess;
using SportsPlatform.Modules.Tenancy.Features.Domains;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;
using SportsPlatform.Modules.Tenancy.Features.Entitlements.GrantOverride;
using SportsPlatform.Modules.Tenancy.Features.Entitlements.RemoveOverride;
using SportsPlatform.Modules.Tenancy.Features.Plans;
using SportsPlatform.Modules.Tenancy.Features.Plans.ListPlans;
using SportsPlatform.Modules.Tenancy.Features.Settings;
using SportsPlatform.Modules.Tenancy.Features.Settings.GetTenantSettings;
using SportsPlatform.Modules.Tenancy.Features.Settings.UpdateTenantSettings;
using SportsPlatform.Modules.Tenancy.Features.Site.GetSite;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.AssignPeriod;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.CancelSubscription;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.ChangePlan;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.GetSubscription;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.RenewSubscription;
using SportsPlatform.Modules.Tenancy.Features.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants.ChangeTenantStatus;
using SportsPlatform.Modules.Tenancy.Features.Tenants.CreateTenant;
using SportsPlatform.Modules.Tenancy.Features.Tenants.GetTenant;
using SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants.PublishTenantEvents;
using SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;
using SportsPlatform.Modules.Tenancy.Infrastructure;
using SportsPlatform.Modules.Tenancy.Seed.Configuration;
using SportsPlatform.Modules.Tenancy.Seed.Demo;

namespace SportsPlatform.Modules.Tenancy;

/// <summary>
/// Tenancy &amp; Commercial: tenants and their lifecycle, domains, settings and the public site (S02); entitlements and plans
/// subscriptions and overrides (S04) (docs/architecture/modules.md).
/// </summary>
public sealed class TenancyModule : IModule
{
    public const string SchemaName = "tenancy";

    // The automated tests' environment (SeedEnvironmentPolicy.Testing).
    private const string _testingEnvironment = "Testing";

    public string Name => "Tenancy";

    public string Schema => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TenancyDbContext>(configuration, Schema);
        services.AddOptions<PlatformDomainOptions>()
            .Bind(configuration.GetSection(PlatformDomainOptions.SectionName))
            // Room for "<longest slug>." in front: every platform subdomain must be a valid host too.
            .Validate(
                o => TenantHost.IsValid(o.BaseDomain) && o.BaseDomain.Length <= TenantHost.MaxLength - TenantSlug.MaxLength - 1,
                "Platform:BaseDomain must be a lower-case host name without a port, short enough for '<slug>.' in front.")
            // The default suits development only: elsewhere every new or backfilled tenant would get a permanent
            // <slug>.localhost domain. Checked on first use too, so the CLI's migrate (backfill) refuses as well.
            .Validate<IHostEnvironment>(
                (o, environment) => environment.IsDevelopment() || environment.IsEnvironment(_testingEnvironment) || !PlatformDomainOptions.IsLocal(o.BaseDomain),
                "Platform:BaseDomain must be configured (not localhost) outside Development and Testing.")
            .ValidateOnStart();
        services.AddSingleton<TenantHostCache>();
        services.AddPostMigrationStep<BackfillPlatformSubdomains>();

        services.AddSingleton<TenantAccessCache>();
        services.AddScoped<TenantAccessReader>();
        services.AddScoped<IEntitlements, TenantEntitlements>();
        services.AddScoped<TenantDirectory>();
        services.AddScoped<ITenantDirectory>(provider => provider.GetRequiredService<TenantDirectory>());
        services.AddScoped<ITenantAccess>(provider => provider.GetRequiredService<TenantDirectory>());
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<IValidator<CreateTenantRequest>, CreateTenantRequestValidator>();
        services.AddScoped<ListTenantsHandler>();
        services.AddSingleton<IValidator<ListTenantsRequest>, ListTenantsRequestValidator>();
        services.AddScoped<GetTenantHandler>();
        services.AddScoped<UpdateTenantProfileHandler>();
        services.AddScoped<IValidator<UpdateTenantProfileRequest>, UpdateTenantProfileRequestValidator>();
        services.AddScoped<ChangeTenantStatusHandler>();
        services.AddScoped<GetSiteHandler>();
        services.AddScoped<GetTenantAccessHandler>();
        services.AddScoped<GetTenantSettingsHandler>();
        services.AddScoped<UpdateTenantSettingsHandler>();
        services.AddScoped<IValidator<UpdateTenantSettingsRequest>, UpdateTenantSettingsRequestValidator>();
        services.AddSingleton<IValidator<ChangeTenantStatusWithReasonRequest>, ChangeTenantStatusWithReasonRequestValidator>();
        services.AddSingleton<IPlanCatalogue>(new PlanCatalogue(TenancyPlans.All));
        services.AddScoped<ListPlansHandler>();
        services.AddScoped<SubscriptionChanges>();
        services.AddScoped<GetSubscriptionHandler>();
        services.AddScoped<AssignPeriodHandler>();
        services.AddScoped<IValidator<AssignPeriodRequest>, AssignPeriodRequestValidator>();
        services.AddScoped<RenewSubscriptionHandler>();
        services.AddScoped<IValidator<RenewSubscriptionRequest>, RenewSubscriptionRequestValidator>();
        services.AddScoped<ChangePlanHandler>();
        services.AddScoped<IValidator<ChangePlanRequest>, ChangePlanRequestValidator>();
        services.AddScoped<CancelSubscriptionHandler>();
        services.AddSingleton<IValidator<CancelSubscriptionRequest>, CancelSubscriptionRequestValidator>();
        services.AddScoped<GrantOverrideHandler>();
        services.AddSingleton<IValidator<GrantOverrideRequest>, GrantOverrideRequestValidator>();
        services.AddScoped<RemoveOverrideHandler>();
        services.AddSingleton<IValidator<RemoveOverrideRequest>, RemoveOverrideRequestValidator>();
        services.AddSeeder<PlanSeeder>();
        services.AddSeeder<DemoTenantSeeder>();
        services.AddSeeder<DemoSubscriptionSeeder>();
        services.AddDomainEventSubscriber<TenantCreatedDomainEvent, PublishTenantCreated>();
        services.AddDomainEventSubscriber<TenantStatusChangedDomainEvent, PublishTenantStatusChanged>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var tenants = TenantEndpoints.Group(groups.Platform);
        ListTenantsEndpoint.Map(tenants);
        GetTenantEndpoint.Map(tenants);
        CreateTenantEndpoint.Map(tenants);
        UpdateTenantProfileEndpoint.Map(tenants);
        ChangeTenantStatusEndpoints.Map(tenants);
        SubscriptionEndpoints.Map(tenants);
        EntitlementOverrideEndpoints.Map(tenants);
        GetSiteEndpoint.Map(groups.Public);
        var settings = TenantSettingsEndpoints.Group(groups.Admin);
        GetTenantSettingsEndpoint.Map(settings);
        UpdateTenantSettingsEndpoint.Map(settings);
        GetTenantAccessEndpoint.Map(groups.Admin);
        ListPlansEndpoint.Map(PlanEndpoints.Group(groups.Platform));
    }
}
