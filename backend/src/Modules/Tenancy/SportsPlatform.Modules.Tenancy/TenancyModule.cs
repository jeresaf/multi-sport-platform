using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Domains;
using SportsPlatform.Modules.Tenancy.Features.Site.GetSite;
using SportsPlatform.Modules.Tenancy.Features.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants.ChangeTenantStatus;
using SportsPlatform.Modules.Tenancy.Features.Tenants.CreateTenant;
using SportsPlatform.Modules.Tenancy.Features.Tenants.GetTenant;
using SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants.PublishTenantEvents;
using SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy;

/// <summary>
/// Tenancy &amp; Commercial: tenants and their lifecycle, domains and the public site (S02); settings, plans and entitlements follow
/// (docs/architecture/modules.md).
/// </summary>
public sealed class TenancyModule : IModule
{
    public const string SchemaName = "tenancy";

    public string Name => "Tenancy";

    public string Schema => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TenancyDbContext>(configuration, Schema);
        services.AddOptions<PlatformDomainOptions>()
            .Bind(configuration.GetSection(PlatformDomainOptions.SectionName))
            .Validate(o => TenantHost.IsValid(o.BaseDomain), "Platform:BaseDomain must be a lower-case host name without a port.")
            .ValidateOnStart();
        services.AddSingleton<TenantHostCache>();
        services.AddPostMigrationStep<BackfillPlatformSubdomains>();

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
        services.AddSingleton<IValidator<ChangeTenantStatusWithReasonRequest>, ChangeTenantStatusWithReasonRequestValidator>();
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
        GetSiteEndpoint.Map(groups.Public);
    }
}
