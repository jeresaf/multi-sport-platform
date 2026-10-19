using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Platform.Contracts.Audit;
using SportsPlatform.Modules.Platform.Contracts.Email;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Platform.Features.Audit.AppendAuditEntry;
using SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;
using SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;
using SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;
using SportsPlatform.Modules.Platform.Features.Reference;
using SportsPlatform.Modules.Platform.Features.Reference.ListCountries;
using SportsPlatform.Modules.Platform.Features.Reference.ListCurrencies;
using SportsPlatform.Modules.Platform.Features.Reference.ListLanguages;
using SportsPlatform.Modules.Platform.Features.Reference.ListTimeZones;
using SportsPlatform.Modules.Platform.Features.Seeding;
using SportsPlatform.Modules.Platform.Infrastructure;
using SportsPlatform.Modules.Platform.Infrastructure.Email;
using SportsPlatform.Modules.Platform.Infrastructure.Messaging;
using SportsPlatform.Modules.Platform.Seed.Reference;

namespace SportsPlatform.Modules.Platform;

/// <summary>
/// Cross-cutting foundations: reference data, audit, seed bookkeeping, transactional email transport, and the host
/// of the outbox processor that delivers every module's integration events (docs/architecture/modules.md).
/// </summary>
public sealed class PlatformModule : IModule
{
    public const string SchemaName = "platform";

    public string Name => "Platform";

    public string Schema => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PlatformDbContext>(configuration, Schema);
        // The audit trail reads and writes across tenants on sp_platform (S02 D3); PlatformDbContext migrates its table.
        services.AddPlatformServiceDbContext<PlatformAuditDbContext>(configuration, Schema);

        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName));
        services.AddOutboxProcessor();
        services.AddHostedService<OutboxHostedService>();

        services.AddSingleton<IAuditLog, AuditLog>();
        services.AddSingleton<IAuditEventFactory, AuditEventFactory>();
        services.AddIntegrationEventConsumer<AuditRecorded.V1, AuditRecordedConsumer, PlatformAuditDbContext>(AuditRecordedConsumer.Name);
        services.AddScoped<ListAuditEntriesHandler>();
        services.AddSingleton<IValidator<ListAuditEntriesRequest>, ListAuditEntriesRequestValidator>();

        services.AddOutboxDiagnostics();
        services.AddScoped<ListOutboxMessagesHandler>();
        services.AddScoped<RedriveOutboxMessageHandler>();
        services.AddSingleton<IValidator<ListOutboxMessagesRequest>, ListOutboxMessagesRequestValidator>();
        services.AddSingleton<IValidator<RedriveOutboxMessageRequest>, RedriveOutboxMessageRequestValidator>();

        // The seed framework (BuildingBlocks) with Platform's bookkeeping; modules register seeders with AddSeeder.
        services.AddSeedRunner<SeedRunLog>(provider => provider.GetRequiredService<IHostEnvironment>().EnvironmentName);

        // Reference data (countries, currencies, languages, time zones) from the embedded data files.
        services.AddSingleton<ReferenceDataFiles>();
        services.AddSeeder<CountrySeeder>();
        services.AddSeeder<CurrencySeeder>();
        services.AddSeeder<LanguageSeeder>();
        services.AddSeeder<TimeZoneSeeder>();
        services.AddScoped<IReferenceData, ReferenceData>();
        services.AddScoped<ListCountriesHandler>();
        services.AddScoped<ListCurrenciesHandler>();
        services.AddScoped<ListLanguagesHandler>();
        services.AddScoped<ListTimeZonesHandler>();
        services.AddSingleton<IValidator<ListTimeZonesRequest>, ListTimeZonesRequestValidator>();

        // Transactional email transport (S03 D13): consumers send through IEmailSender, never inside a request.
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ListOutboxMessagesEndpoint.Map(groups.Platform);
        RedriveOutboxMessageEndpoint.Map(groups.Platform);
        ListAuditEntriesEndpoint.Map(groups.Platform);

        var reference = ReferenceEndpoints.Group(groups.Public);
        ListCountriesEndpoint.Map(reference);
        ListCurrenciesEndpoint.Map(reference);
        ListLanguagesEndpoint.Map(reference);
        ListTimeZonesEndpoint.Map(reference);
    }
}
