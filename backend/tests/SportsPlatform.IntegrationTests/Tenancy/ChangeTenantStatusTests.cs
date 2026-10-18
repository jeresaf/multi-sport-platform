using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// <c>POST /api/platform/v1/tenants/{tenantId}/{transition}</c>: the tenant state machine over HTTP, with its history row,
/// <c>TenantStatusChanged.V1</c> and an audit entry under the target tenant (S02 D2).
/// </summary>
public sealed class ChangeTenantStatusTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _reason = "Season fees for 2026 unpaid after three reminders";

    private static readonly Dictionary<(TenantStatus From, string Transition), TenantStatus> _allowed = new()
    {
        [(TenantStatus.Provisioning, "activate")] = TenantStatus.Active,
        [(TenantStatus.Active, "suspend")] = TenantStatus.Suspended,
        [(TenantStatus.Suspended, "restore")] = TenantStatus.Active,
        [(TenantStatus.Active, "archive")] = TenantStatus.Archived,
        [(TenantStatus.Suspended, "archive")] = TenantStatus.Archived,
        [(TenantStatus.Archived, "unarchive")] = TenantStatus.Suspended,
    };

    private static readonly string[] _transitions = ["activate", "suspend", "restore", "archive", "unarchive"];

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    public static TheoryData<TenantStatus, string, TenantStatus> AllowedTransitions()
    {
        var data = new TheoryData<TenantStatus, string, TenantStatus>();
        foreach (var ((from, transition), to) in _allowed)
        {
            data.Add(from, transition, to);
        }

        return data;
    }

    public static TheoryData<TenantStatus, string> ForbiddenTransitions()
    {
        var data = new TheoryData<TenantStatus, string>();
        foreach (var status in Enum.GetValues<TenantStatus>())
        {
            foreach (var transition in _transitions.Where(t => !_allowed.ContainsKey((status, t))))
            {
                data.Add(status, transition);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public async Task Each_transition_writes_one_history_row_one_event_and_one_audit_entry_under_the_tenant(
        TenantStatus from, string transition, TenantStatus to)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", from, Ct);
        var historyBefore = (await TenantTestData.HistoryAsync(App.Services, tenantId, Ct)).Count;
        var outboxBefore = (await TenantTestData.OutboxAsync(App.Services, Ct)).Count;
        App.Clock.Advance(TimeSpan.FromDays(1));

        using var response = await PostAsync(tenantId, transition, NeedsReason(transition) ? new { reason = _reason } : null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenant = (await response.Content.ReadFromJsonAsync<TenantResponse>(Json, Ct)).ShouldNotBeNull();
        tenant.Status.ShouldBe(to);
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{tenant.Version}\"");

        var history = await TenantTestData.HistoryAsync(App.Services, tenantId, Ct);
        history.Count.ShouldBe(historyBefore + 1);
        history[^1].ShouldSatisfyAllConditions(
            r => r.FromStatus.ShouldBe(from),
            r => r.ToStatus.ShouldBe(to),
            r => r.Reason.ShouldBe(NeedsReason(transition) ? _reason : null),
            r => r.ActorKind.ShouldBe(ActorKind.User),
            r => r.ActorId.ShouldBe(TenantTestData.StaffId),
            r => r.OccurredAt.ShouldBe(App.Clock.GetUtcNow()));

        var messages = (await TenantTestData.OutboxAsync(App.Services, Ct)).Skip(outboxBefore).ToList();
        messages.Select(m => m.EventName).ShouldBe(["TenantStatusChanged", "AuditRecorded"], ignoreOrder: true);
        messages.ShouldAllBe(m => m.TenantId == tenantId);
        JsonSerializer.Deserialize<TenantStatusChanged.V1>(messages.Single(m => m.EventName == "TenantStatusChanged").Payload, Json)
            .ShouldBe(new TenantStatusChanged.V1(tenantId, from, to));

        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(Ct);
        await processor.ProcessOnceAsync(Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe($"tenant.{transition}"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.TenantId.ShouldBe(tenantId),
            e => e.EntityType.ShouldBe("Tenant"),
            e => e.EntityId.ShouldBe(tenantId),
            e => e.Reason.ShouldBe(NeedsReason(transition) ? _reason : null));
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public async Task Every_other_transition_is_refused(TenantStatus from, string transition)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "elssc", from, Ct);
        var historyBefore = (await TenantTestData.HistoryAsync(App.Services, tenantId, Ct)).Count;

        using var response = await PostAsync(tenantId, transition, NeedsReason(transition) ? new { reason = _reason } : null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_INVALID_TRANSITION");
        (await TenantTestData.HistoryAsync(App.Services, tenantId, Ct)).Count.ShouldBe(historyBefore);
        (await TenantTestData.TenantAsync(App.Services, tenantId, Ct)).Status.ShouldBe(from);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.Outcome.ShouldBe(AuditOutcome.Failure);
        entry.TenantId.ShouldBe(tenantId);
    }

    public static TheoryData<string, string> MissingReasons()
    {
        var data = new TheoryData<string, string>();
        foreach (var transition in new[] { "suspend", "archive", "unarchive" })
        {
            data.Add(transition, "{}");
            data.Add(transition, """{"reason":""}""");
            data.Add(transition, """{"reason":"   "}""");
            data.Add(transition, $$"""{"reason":"{{new string('x', 501)}}"}""");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MissingReasons))]
    public async Task A_missing_or_unusable_reason_is_refused(string transition, string body)
    {
        var from = transition == "unarchive" ? TenantStatus.Archived : TenantStatus.Active;
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", from, Ct);

        using var response = await PostJsonAsync(tenantId, transition, body);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        CreateTenantTests.Errors(problem).ShouldBe([("reason", "VALIDATION_REASON_REQUIRED")]);
        (await TenantTestData.TenantAsync(App.Services, tenantId, Ct)).Status.ShouldBe(from);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task A_reason_transition_without_a_body_is_malformed(string transition)
    {
        var from = transition == "unarchive" ? TenantStatus.Archived : TenantStatus.Active;
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", from, Ct);

        using var response = await PostJsonAsync(tenantId, transition, null);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        (await TenantTestData.TenantAsync(App.Services, tenantId, Ct)).Status.ShouldBe(from);
    }

    [Fact]
    public async Task The_reason_is_stored_trimmed_and_may_be_500_characters()
    {
        var active = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var archived = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Archived, Ct);
        var longest = new string('x', 500);

        using var suspended = await PostAsync(active, "suspend", new { reason = $"  {_reason}  " });
        using var unarchived = await PostAsync(archived, "unarchive", new { reason = longest });

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        unarchived.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TenantTestData.HistoryAsync(App.Services, active, Ct))[^1].Reason.ShouldBe(_reason);
        (await TenantTestData.HistoryAsync(App.Services, archived, Ct))[^1].Reason.ShouldBe(longest);
        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(Ct);
        await processor.ProcessOnceAsync(Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.TenantId == active).Reason.ShouldBe(_reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Activate_and_restore_take_an_optional_body(string? body)
    {
        var provisioning = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Provisioning, Ct);
        var suspended = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Suspended, Ct);

        using var activated = await PostJsonAsync(provisioning, "activate", body);
        using var restored = await PostJsonAsync(suspended, "restore", body);

        activated.StatusCode.ShouldBe(HttpStatusCode.OK);
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TenantTestData.TenantAsync(App.Services, provisioning, Ct)).Status.ShouldBe(TenantStatus.Active);
        (await TenantTestData.TenantAsync(App.Services, suspended, Ct)).Status.ShouldBe(TenantStatus.Active);
    }

    [Fact]
    public async Task Activate_refuses_a_reason_it_does_not_take()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Provisioning, Ct);

        using var response = await PostAsync(tenantId, "activate", new { reason = _reason });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        (await TenantTestData.TenantAsync(App.Services, tenantId, Ct)).Status.ShouldBe(TenantStatus.Provisioning);
    }

    [Fact]
    public async Task Unknown_tenant_is_not_found()
    {
        using var response = await PostAsync(new Guid("0192a1b2-0000-7000-8000-00000000c404"), "suspend", new { reason = _reason });

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task A_stale_version_in_the_body_conflicts()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var version = (await TenantTestData.TenantAsync(App.Services, tenantId, Ct)).Version;

        using var stale = await PostAsync(tenantId, "suspend", new { reason = _reason, version = version + 1 });
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_CONCURRENCY_CONFLICT");

        using var current = await PostAsync(tenantId, "suspend", new { reason = _reason, version });
        current.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("current", null, HttpStatusCode.OK)]
    [InlineData("*", null, HttpStatusCode.OK)]
    [InlineData("stale", null, HttpStatusCode.Conflict)]
    [InlineData("current", "stale", HttpStatusCode.Conflict)]
    [InlineData("stale", "current", HttpStatusCode.Conflict)]
    [InlineData("current", "current", HttpStatusCode.OK)]
    public async Task If_match_and_body_version_must_both_be_current(string ifMatch, string? bodyVersion, HttpStatusCode expected)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Provisioning, Ct);
        var version = (await TenantTestData.TenantAsync(App.Services, tenantId, Ct)).Version;
        uint? Pick(string? which) => which switch { "current" => version, "stale" => version + 1, _ => null };
        var header = ifMatch == "*" ? "*" : $"\"{Pick(ifMatch)}\"";

        using var response = await PostJsonAsync(
            tenantId, "activate", bodyVersion is null ? null : $$"""{"version":{{Pick(bodyVersion)}}}""", header);

        if (expected == HttpStatusCode.OK)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await response.ShouldBeProblemAsync(expected, "TENANT_CONCURRENCY_CONFLICT");
            (await TenantTestData.TenantAsync(App.Services, tenantId, Ct)).Status.ShouldBe(TenantStatus.Provisioning);
        }
    }

    [Fact]
    public async Task A_malformed_if_match_is_refused()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Provisioning, Ct);

        using var response = await PostJsonAsync(tenantId, "activate", null, "1234");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    private static bool NeedsReason(string transition) => transition is "suspend" or "archive" or "unarchive";

    private Task<HttpResponseMessage> PostAsync(Guid tenantId, string transition, object? body) =>
        PostJsonAsync(tenantId, transition, body is null ? null : JsonSerializer.Serialize(body));

    private async Task<HttpResponseMessage> PostJsonAsync(Guid tenantId, string transition, string? json, string? ifMatch = null)
    {
        App.Authorization.AllowAll();
        App.Actor.ActAsUser(TenantTestData.StaffId);
        using var client = App.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"{TenantTestData.TenantsPath}/{tenantId}/{transition}", UriKind.Relative));
        if (json is not null)
        {
            request.Content = new StringContent(json, new MediaTypeHeaderValue("application/json"));
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, Ct);
    }
}
