using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Platform.Contracts.Audit;
using SportsPlatform.Modules.Platform.Features.Audit.AppendAuditEntry;
using SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;
using SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;
using SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;
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

        services.AddSingleton<IAuditLog, AuditLog>();
        services.AddSingleton<IAuditEventFactory, AuditEventFactory>();
        services.AddIntegrationEventConsumer<AuditRecorded.V1, AuditRecordedConsumer, PlatformDbContext>(AuditRecordedConsumer.Name);
        services.AddScoped<ListAuditEntriesHandler>();
        services.AddSingleton<IValidator<ListAuditEntriesRequest>, ListAuditEntriesRequestValidator>();

        services.AddOutboxDiagnostics();
        services.AddScoped<ListOutboxMessagesHandler>();
        services.AddScoped<RedriveOutboxMessageHandler>();
        services.AddSingleton<IValidator<ListOutboxMessagesRequest>, ListOutboxMessagesRequestValidator>();
        services.AddSingleton<IValidator<RedriveOutboxMessageRequest>, RedriveOutboxMessageRequestValidator>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ListOutboxMessagesEndpoint.Map(groups.Platform);
        RedriveOutboxMessageEndpoint.Map(groups.Platform);
        ListAuditEntriesEndpoint.Map(groups.Platform);
    }
}
