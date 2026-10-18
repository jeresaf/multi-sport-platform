using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Host.Api;
using SportsPlatform.IntegrationTests.Authorization;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Messaging;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Contracts.Audit;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;

namespace SportsPlatform.IntegrationTests.Audit;

/// <summary>
/// The automatic audit (AR-19): an audited action writes exactly one entry, through the changing module's outbox when it
/// saves, directly when it is denied, fails or saves nothing; metadata is redacted (AR-24).
/// </summary>
public sealed class AuditTrailTests : DatabaseTest
{
    private const string _outboxPath = "/api/platform/v1/diagnostics/outbox";
    private const string _reason = "Kawempe fixture consumer fixed in release 1.4";
    private const string _correlationId = "kmsa-req-0042";
    private static readonly Guid _staffId = new("0192a1b2-0000-7000-8000-00000000a001");

    public AuditTrailTests(PostgresFixture fixture)
        : base(fixture) => App.Consumers.Reset();

    private OutboxProcessor Processor => App.Services.GetRequiredService<OutboxProcessor>();

    [Fact]
    public async Task Redrive_is_audited_once_with_the_correlation_ID_and_reason()
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        var failedId = await FailedMessageAsync();

        using var response = await RedriveAsync(failedId, new { reason = _reason });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // Committed with the re-drive, in the target module's outbox; appended when the processor delivers it.
        var auditMessage = (await AuditMessagesAsync()).ShouldHaveSingleItem();
        auditMessage.CorrelationId.ShouldBe(_correlationId);
        (await EntriesAsync()).ShouldBeEmpty();

        await Processor.ProcessOnceAsync(Ct);
        await Processor.ProcessOnceAsync(Ct);

        var entry = (await EntriesAsync()).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("platform.outbox.redrive"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.ErrorCode.ShouldBeNull(),
            e => e.CorrelationId.ShouldBe(_correlationId),
            e => e.Reason.ShouldBe(_reason),
            e => e.ActorKind.ShouldBe(ActorKind.User),
            e => e.ActorId.ShouldBe(_staffId),
            e => e.TenantId.ShouldBeNull(),
            e => e.Source.ShouldBe(AuditSource.Api),
            e => e.EntityType.ShouldBe(RedriveOutboxMessageHandler.AuditEntityType),
            e => e.EntityId.ShouldBe(failedId),
            e => e.OccurredAt.ShouldBe(App.Clock.GetUtcNow()),
            e => e.Id.ShouldBe(auditMessage.AggregateId));
        Metadata(entry.Metadata).GetProperty("module").GetString().ShouldBe(MessagingTestDbContext.SchemaName);
    }

    [Fact]
    public async Task List_saves_nothing_so_it_is_audited_directly_with_its_filters()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri($"{_outboxPath}?status=FAILED", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var entry = (await EntriesAsync()).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("platform.outbox.read"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.CorrelationId.ShouldBe(_correlationId),
            e => e.Reason.ShouldBeNull(),
            e => e.EntityType.ShouldBeNull());
        var metadata = Metadata(entry.Metadata);
        metadata.GetProperty("status").GetString().ShouldBe("FAILED");
        metadata.GetProperty("module").ValueKind.ShouldBe(JsonValueKind.Null);
        (await AuditMessagesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Redrive_that_changes_nothing_is_audited_as_a_failure_with_its_error_code()
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        var pendingId = await PendingMessageAsync();

        using var response = await RedriveAsync(pendingId, new { reason = _reason });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AuditMessagesAsync()).ShouldBeEmpty();
        (await EntriesAsync()).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Failure),
            e => e.ErrorCode.ShouldBe(PlatformErrors.OutboxMessageNotFailed.Code),
            e => e.Reason.ShouldBe(_reason),
            e => e.EntityId.ShouldBe(pendingId),
            e => e.CorrelationId.ShouldBe(_correlationId));
    }

    [Fact]
    public async Task Invalid_request_is_audited_as_a_failure()
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        var failedId = await FailedMessageAsync();

        using var response = await RedriveAsync(failedId, new { reason = "  " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await EntriesAsync()).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Failure),
            e => e.ErrorCode.ShouldBe("VALIDATION_REASON_REQUIRED"),
            e => e.Reason.ShouldBeNull());
    }

    [Fact]
    public async Task Denied_redrive_is_audited_as_denied()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        var failedId = await FailedMessageAsync();

        using var response = await RedriveAsync(failedId, new { reason = _reason });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await AuditMessagesAsync()).ShouldBeEmpty();
        (await EntriesAsync()).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("platform.outbox.redrive"),
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.ErrorCode.ShouldBe("AUTH_FORBIDDEN"),
            e => e.ActorId.ShouldBe(_staffId),
            e => e.Reason.ShouldBeNull(), // the body is never read for a caller who is not allowed
            e => e.CorrelationId.ShouldBe(_correlationId));
    }

    [Fact]
    public async Task First_save_in_the_scope_records_the_entry_and_later_saves_do_not()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var audit = BeginAudit(scope);
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();

        context.Probes.Add(new MessagingProbe("Kisaasi Recreation Ground"));
        await context.SaveChangesAsync(Ct);
        context.Probes.Add(new MessagingProbe("Nakivubo Memorial Ground"));
        await context.SaveChangesAsync(Ct);

        audit.IsRecorded.ShouldBeTrue();
        var message = (await AuditMessagesAsync()).ShouldHaveSingleItem();
        message.EventName.ShouldBe(nameof(AuditRecorded));
        message.AggregateType.ShouldBe(IAuditEventFactory.AggregateType);
    }

    [Fact]
    public async Task Failed_save_commits_no_audit_message_and_leaves_the_entry_pending()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var audit = BeginAudit(scope);
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();

        context.Probes.Add(new MessagingProbe(new string('K', 201))); // longer than the column allows
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

        audit.IsPending.ShouldBeTrue();
        context.ChangeTracker.Entries<OutboxMessage>()
            .ShouldNotContain(entry => entry.Entity.EventName == nameof(AuditRecorded) && entry.State == EntityState.Added);
        (await AuditMessagesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Personal_data_in_metadata_is_masked_in_the_outbox_and_the_entry()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            BeginAudit(scope).Describe(
                "Person", Guid.CreateVersion7(), "Guardian details corrected",
                new PersonAuditMetadata("Aisha Nakato", "aisha.nakato@example.ug", "CM9001234567XY", new(2011, 3, 14)));
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            context.Probes.Add(new MessagingProbe("Kisaasi Recreation Ground"));
            await context.SaveChangesAsync(Ct);
        }

        var payload = (await AuditMessagesAsync()).ShouldHaveSingleItem().Payload;
        payload.ShouldNotContain("aisha.nakato@example.ug");
        payload.ShouldNotContain("CM9001234567XY");
        payload.ShouldNotContain("2011-03-14");

        await Processor.ProcessOnceAsync(Ct);
        var metadata = Metadata((await EntriesAsync()).ShouldHaveSingleItem().Metadata);
        metadata.GetProperty("displayName").GetString().ShouldBe("Aisha Nakato");
        metadata.GetProperty("email").GetString().ShouldBe(AuditMetadata.Redacted);
        metadata.GetProperty("nationalId").GetString().ShouldBe(AuditMetadata.Redacted);
        metadata.GetProperty("dateOfBirth").GetString().ShouldBe(AuditMetadata.Redacted);
    }

    public sealed record PersonAuditMetadata(
        [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
        [property: PersonalData(PersonalDataClass.Private)] string Email,
        [property: PersonalData(PersonalDataClass.Sensitive)] string NationalId,
        [property: PersonalData(PersonalDataClass.Private)] DateOnly DateOfBirth);

    private static AuditScope BeginAudit(AsyncServiceScope scope)
    {
        var audit = scope.ServiceProvider.GetRequiredService<AuditScope>();
        audit.Begin(AuthorizationTestActions.FixtureCreate, AuditSource.Api);
        return audit;
    }

    private static JsonElement Metadata(string? json) => JsonDocument.Parse(json.ShouldNotBeNull()).RootElement;

    /// <summary>Requests from a platform staff member, with a known correlation ID.</summary>
    private HttpClient CreateClient()
    {
        App.Actor.ActAsUser(_staffId);
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, _correlationId);
        return client;
    }

    private async Task<HttpResponseMessage> RedriveAsync(Guid messageId, object body)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync(
            new Uri($"{_outboxPath}/{MessagingTestDbContext.SchemaName}/{messageId}/redrive", UriKind.Relative), body, Ct);
    }

    private Task<List<AuditEntry>> EntriesAsync() => AuditTestData.EntriesAsync(App.Services, Ct);

    private async Task<List<OutboxMessage>> AuditMessagesAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>().Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventName == nameof(AuditRecorded)).ToListAsync(Ct);
    }

    private async Task<Guid> PendingMessageAsync()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            context.Probes.Add(new MessagingProbe("Kisaasi Recreation Ground"));
            await context.SaveChangesAsync(Ct);
        }

        await using var read = App.Services.CreateAsyncScope();
        return await read.ServiceProvider.GetRequiredService<MessagingTestDbContext>().Set<OutboxMessage>()
            .Select(m => m.Id).SingleAsync(Ct);
    }

    /// <summary>A probe's message that the flaky consumer failed until it is <c>FAILED</c>.</summary>
    private async Task<Guid> FailedMessageAsync()
    {
        var messageId = await PendingMessageAsync();
        App.Consumers.FlakyFailuresLeft = int.MaxValue;
        var maxAttempts = App.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.MaxAttempts;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await Processor.ProcessOnceAsync(Ct);
            App.Clock.Advance(OutboxRetry.MaxDelay);
        }

        App.Consumers.FlakyFailuresLeft = 0;
        App.Authorization.Requests.Clear();
        return messageId;
    }
}
