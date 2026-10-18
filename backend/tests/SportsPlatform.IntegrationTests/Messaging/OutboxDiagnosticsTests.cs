using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Persistence;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Features.Outbox;
using SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;

namespace SportsPlatform.IntegrationTests.Messaging;

/// <summary>The platform's outbox diagnostics: listing every module's messages and re-driving a FAILED one.</summary>
public sealed class OutboxDiagnosticsTests : DatabaseTest
{
    private const string _listPath = "/api/platform/v1/diagnostics/outbox";
    private const string _reason = "Kawempe fixture consumer fixed in release 1.4";

    public OutboxDiagnosticsTests(PostgresFixture fixture)
        : base(fixture) => App.Consumers.Reset();

    private OutboxProcessor Processor => App.Services.GetRequiredService<OutboxProcessor>();

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task Redriven_message_is_delivered_after_the_fix_and_releases_its_aggregate()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead, PlatformActions.OutboxRedrive);
        var probeId = await CreateProbeAsync();
        await PublishAsync(probeId, "Kisaasi Recreation Ground, renamed");
        await FailFirstMessageAsync();
        var failed = (await ListAsync("?status=FAILED")).Items.ShouldHaveSingleItem();

        App.Consumers.FlakyFailuresLeft = 0; // the fix
        using var client = App.CreateClient();
        using var response = await RedriveAsync(client, MessagingTestDbContext.SchemaName, failed.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var redriven = (await response.Content.ReadFromJsonAsync<OutboxMessageResponse>(Json, Ct)).ShouldNotBeNull();
        redriven.ShouldSatisfyAllConditions(
            m => m.Status.ShouldBe(OutboxMessageStatus.Pending),
            m => m.Attempts.ShouldBe(0),
            m => m.NextAttemptAt.ShouldBe(App.Clock.GetUtcNow()),
            m => m.LastError.ShouldBe(failed.LastError)); // kept until the next outcome

        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1); // the re-driven message
        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1); // then the one it held back
        (await ListAsync()).Items.ShouldAllBe(m => m.Status == OutboxMessageStatus.Processed && m.LastError == null);
    }

    [Fact]
    public async Task List_shows_the_delivery_state_without_the_payload()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        await CreateProbeAsync();
        await FailFirstMessageAsync();
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri($"{_listPath}?status=FAILED", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("items")[0];
        item.TryGetProperty("payload", out _).ShouldBeFalse();
        item.GetProperty("module").GetString().ShouldBe(MessagingTestDbContext.SchemaName);
        item.GetProperty("eventName").GetString().ShouldBe(nameof(ProbeCreated));
        item.GetProperty("status").GetString().ShouldBe("FAILED"); // enums are UPPER_SNAKE strings
        item.GetProperty("attempts").GetInt32().ShouldBe(MaxAttempts());
        item.GetProperty("lastError").GetString().ShouldNotBeNull().ShouldContain(FlakyConsumer.Name);
    }

    [Fact]
    public async Task Pending_message_behind_a_failed_one_shows_what_holds_it_back()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        var probeId = await CreateProbeAsync();
        await PublishAsync(probeId, "Kisaasi Recreation Ground, renamed");
        await FailFirstMessageAsync();
        var otherProbeId = await CreateProbeAsync(); // another aggregate: pending, but not held back

        var items = (await ListAsync()).Items;

        var failed = items.Where(m => m.Status == OutboxMessageStatus.Failed).ShouldHaveSingleItem();
        failed.HeldBackBy.ShouldBeNull();
        items.Where(m => m.AggregateId == probeId && m.Status == OutboxMessageStatus.Pending).ShouldHaveSingleItem()
            .HeldBackBy.ShouldBe(failed.Id);
        items.Where(m => m.AggregateId == otherProbeId).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            m => m.Status.ShouldBe(OutboxMessageStatus.Pending),
            m => m.HeldBackBy.ShouldBeNull());
    }

    [Fact]
    public async Task List_pages_newest_first_across_modules_and_filters_by_module_and_status()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        var published = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            published.Add(await PublishInConventionsModuleAsync());
            await CreateProbeAsync();
        }

        var all = (await ListAsync()).Items;
        all.Count.ShouldBe(6);
        all.Select(m => m.Id).ShouldBe(all.Select(m => m.Id).OrderDescending()); // newest first
        all.Select(m => m.Module).Distinct().Order().ShouldBe([ConventionsTestDbContext.SchemaName, MessagingTestDbContext.SchemaName]);

        var first = await ListAsync("?limit=4");
        first.Items.Count.ShouldBe(4);
        first.Page.HasMore.ShouldBeTrue();
        var second = await ListAsync($"?limit=4&cursor={first.Page.NextCursor}");
        second.Items.Count.ShouldBe(2);
        second.Page.ShouldBe(new(null, false));
        first.Items.Concat(second.Items).Select(m => m.Id).ShouldBe(all.Select(m => m.Id));

        (await ListAsync($"?module={ConventionsTestDbContext.SchemaName}")).Items.Select(m => m.Id)
            .ShouldBe(Enumerable.Reverse(published));
        (await ListAsync("?status=PROCESSED")).Items.ShouldBeEmpty();
        (await ListAsync("?status=PENDING")).Items.Count.ShouldBe(6);
    }

    [Theory]
    [InlineData("?status=failed", "status")]
    [InlineData("?status=RETRYING", "status")]
    [InlineData("?module=registry", "module")]
    [InlineData("?limit=0", "limit")]
    [InlineData("?limit=101", "limit")]
    [InlineData("?limit=abc", "limit")]
    [InlineData("?limit=+5", "limit")]
    [InlineData("?limit=", "limit")]
    [InlineData("?cursor=not-a-cursor", "cursor")]
    public async Task Invalid_query_is_rejected(string query, string field)
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri(_listPath + query, UriKind.Relative), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        problem.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
        ShouldNameField(problem, field, "VALIDATION_INVALID_QUERY");
    }

    [Fact]
    public async Task Every_invalid_parameter_is_listed()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri($"{_listPath}?status=RETRYING&limit=0", UriKind.Relative), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ShouldBe(["status", "limit"]);
    }

    [Fact]
    public async Task Only_a_failed_message_can_be_redriven()
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        await CreateProbeAsync();
        var pending = (await OutboxAsync()).ShouldHaveSingleItem();
        using var client = App.CreateClient();

        using var response = await RedriveAsync(client, MessagingTestDbContext.SchemaName, pending.Id);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PLATFORM_OUTBOX_MESSAGE_NOT_FAILED");
    }

    [Fact]
    public async Task Redriving_twice_conflicts()
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        await CreateProbeAsync();
        await FailFirstMessageAsync();
        var failed = (await OutboxAsync()).ShouldHaveSingleItem();
        using var client = App.CreateClient();

        using (var first = await RedriveAsync(client, MessagingTestDbContext.SchemaName, failed.Id))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var second = await RedriveAsync(client, MessagingTestDbContext.SchemaName, failed.Id);
        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PLATFORM_OUTBOX_MESSAGE_NOT_FAILED");
    }

    [Fact]
    public async Task Unknown_message_or_module_is_not_found()
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        await CreateProbeAsync();
        await FailFirstMessageAsync();
        var failed = (await OutboxAsync()).ShouldHaveSingleItem();
        using var client = App.CreateClient();

        using var unknownId = await RedriveAsync(client, MessagingTestDbContext.SchemaName, Guid.CreateVersion7());
        await unknownId.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PLATFORM_OUTBOX_MESSAGE_NOT_FOUND");
        using var otherModule = await RedriveAsync(client, ConventionsTestDbContext.SchemaName, failed.Id);
        await otherModule.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PLATFORM_OUTBOX_MESSAGE_NOT_FOUND");
        using var unknownModule = await RedriveAsync(client, "registry", failed.Id);
        await unknownModule.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PLATFORM_OUTBOX_MESSAGE_NOT_FOUND");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"reason":null}""")]
    [InlineData("""{"reason":"   "}""")]
    [InlineData("TOO_LONG")]
    public async Task Redrive_needs_a_reason(string body)
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        await CreateProbeAsync();
        await FailFirstMessageAsync();
        var failed = (await OutboxAsync()).ShouldHaveSingleItem();
        if (body == "TOO_LONG")
        {
            body = JsonSerializer.Serialize(new { reason = new string('x', 501) });
        }

        using var client = App.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(RedrivePath(MessagingTestDbContext.SchemaName, failed.Id), content, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        ShouldNameField(problem, "reason", "VALIDATION_REASON_REQUIRED");
        (await OutboxAsync()).ShouldHaveSingleItem().Status.ShouldBe(OutboxMessageStatus.Failed);
    }

    [Fact]
    public async Task List_is_denied_without_the_read_action()
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri(_listPath, UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        App.Authorization.Requests.ShouldHaveSingleItem().Action.ShouldBe(PlatformActions.OutboxRead);
    }

    [Fact]
    public async Task List_is_allowed_with_the_read_action()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri(_listPath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        App.Authorization.Requests.ShouldHaveSingleItem().Action.ShouldBe(PlatformActions.OutboxRead);
    }

    [Fact]
    public async Task Redrive_is_denied_with_only_the_read_action()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        await CreateProbeAsync();
        await FailFirstMessageAsync();
        var failed = (await OutboxAsync()).ShouldHaveSingleItem();
        using var client = App.CreateClient();

        using var response = await RedriveAsync(client, MessagingTestDbContext.SchemaName, failed.Id);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        App.Authorization.Requests.ShouldHaveSingleItem().Action.ShouldBe(PlatformActions.OutboxRedrive);
        (await OutboxAsync()).ShouldHaveSingleItem().Status.ShouldBe(OutboxMessageStatus.Failed);
    }

    [Fact]
    public async Task Redrive_is_allowed_with_the_redrive_action()
    {
        App.Authorization.Allow(PlatformActions.OutboxRedrive);
        await CreateProbeAsync();
        await FailFirstMessageAsync();
        var failed = (await OutboxAsync()).ShouldHaveSingleItem();
        using var client = App.CreateClient();

        using var response = await RedriveAsync(client, MessagingTestDbContext.SchemaName, failed.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        App.Authorization.Requests.ShouldHaveSingleItem().Action.ShouldBe(PlatformActions.OutboxRedrive);
    }

    private static void ShouldNameField(JsonElement problem, string field, string code)
    {
        var error = problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("field").GetString().ShouldBe(field);
        error.GetProperty("code").GetString().ShouldBe(code);
    }

    /// <summary>Runs the processor until the first due message is FAILED (the flaky consumer keeps failing).</summary>
    private async Task FailFirstMessageAsync()
    {
        App.Consumers.FlakyFailuresLeft = int.MaxValue;
        for (var attempt = 1; attempt <= MaxAttempts(); attempt++)
        {
            await Processor.ProcessOnceAsync(Ct);
            App.Clock.Advance(OutboxRetry.MaxDelay);
        }

        App.Consumers.FlakyFailuresLeft = 0;
        App.Authorization.Requests.Clear();
    }

    private async Task<ListOutboxMessagesResponse> ListAsync(string query = "")
    {
        using var client = App.CreateClient();
        using var response = await client.GetAsync(new Uri(_listPath + query, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ListOutboxMessagesResponse>(Json, Ct)).ShouldNotBeNull();
    }

    private static Uri RedrivePath(string module, Guid messageId) =>
        new($"{_listPath}/{module}/{messageId}/redrive", UriKind.Relative);

    private static Task<HttpResponseMessage> RedriveAsync(HttpClient client, string module, Guid messageId) =>
        client.PostAsJsonAsync(RedrivePath(module, messageId), new { reason = _reason }, Ct);

    private int MaxAttempts() =>
        App.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.MaxAttempts;

    private async Task<Guid> CreateProbeAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        var probe = new MessagingProbe("Kisaasi Recreation Ground");
        context.Probes.Add(probe);
        await context.SaveChangesAsync(Ct);
        return probe.Id;
    }

    private async Task PublishAsync(Guid probeId, string name)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        context.Publish(new ProbeCreated.V1(probeId, name), nameof(MessagingProbe), probeId);
        await context.SaveChangesAsync(Ct);
    }

    /// <summary>A message in a second module's outbox, so the list merges two schemas.</summary>
    private async Task<Guid> PublishInConventionsModuleAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>();
        var probeId = Guid.CreateVersion7();
        context.Publish(new ProbeCreated.V1(probeId, "Nakivubo Memorial Ground"), nameof(ConventionProbe), probeId);
        await context.SaveChangesAsync(Ct);
        return (await OutboxAsync<ConventionsTestDbContext>()).Single(m => m.AggregateId == probeId).Id;
    }

    private Task<List<OutboxMessage>> OutboxAsync() => OutboxAsync<MessagingTestDbContext>();

    private async Task<List<OutboxMessage>> OutboxAsync<TContext>()
        where TContext : ModuleDbContext
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TContext>().Set<OutboxMessage>().AsNoTracking().ToListAsync(Ct);
    }
}
