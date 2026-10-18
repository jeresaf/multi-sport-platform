using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants.CreateTenant;
using SportsPlatform.Modules.Tenancy.Features.Tenants.GetTenant;
using SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants.PublishTenantEvents;
using SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy;

/// <summary>
/// Tenancy &amp; Commercial: tenants and their lifecycle (S02); domains, settings, plans and entitlements follow
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

        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<IValidator<CreateTenantRequest>, CreateTenantRequestValidator>();
        services.AddScoped<ListTenantsHandler>();
        services.AddSingleton<IValidator<ListTenantsRequest>, ListTenantsRequestValidator>();
        services.AddScoped<GetTenantHandler>();
        services.AddScoped<UpdateTenantProfileHandler>();
        services.AddScoped<IValidator<UpdateTenantProfileRequest>, UpdateTenantProfileRequestValidator>();
        services.AddDomainEventSubscriber<TenantCreatedDomainEvent, PublishTenantCreated>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var tenants = TenantEndpoints.Group(groups.Platform);
        ListTenantsEndpoint.Map(tenants);
        GetTenantEndpoint.Map(tenants);
        CreateTenantEndpoint.Map(tenants);
        UpdateTenantProfileEndpoint.Map(tenants);
    }
}
