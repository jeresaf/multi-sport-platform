using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Platform.Infrastructure;
using SportsPlatform.Modules.Platform.Infrastructure.Messaging;

namespace SportsPlatform.Modules.Platform;

/// <summary>
/// Cross-cutting foundations: reference data, audit, seed bookkeeping, and the host of the outbox processor that
/// delivers every module's integration events (docs/architecture/modules.md).
/// </summary>
public sealed class PlatformModule : IModule
{
    public const string SchemaName = "platform";

    public string Name => "Platform";

    public string Schema => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PlatformDbContext>(configuration, Schema);

        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName));
        services.AddOutboxProcessor();
        services.AddHostedService<OutboxHostedService>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
    }
}
