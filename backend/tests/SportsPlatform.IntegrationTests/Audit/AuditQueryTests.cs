using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Contracts.Audit;
using SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;

namespace SportsPlatform.IntegrationTests.Audit;

/// <summary>The audit list for platform staff: filters, newest-first cursor paging, and its own audit.</summary>
public sealed class AuditQueryTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _listPath = "/api/platform/v1/audit-entries";
    private const string _kmsa = "0192a1b2-0000-7000-8000-00000000c001";
    private const string _elssc = "0192a1b2-0000-7000-8000-00000000c002";
    private const string _registrar = "0192a1b2-0000-7000-8000-00000000a011";
    private const string _competitionManager = "0192a1b2-0000-7000-8000-00000000a012";
    private const string _person = "0192a1b2-0000-7000-8000-00000000e001";
    private static readonly Guid _staffId = new("0192a1b2-0000-7000-8000-00000000a001");

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    /// <summary>Four entries, an hour apart from 09:00Z on 2026-10-17; the expected letters are newest first.</summary>
    [Theory]
    [InlineData("tenantId=" + _kmsa, "b,a")]
    [InlineData("actorId=" + _registrar, "c,a")]
    [InlineData("action=person.update", "b")]
    [InlineData("entityType=Person", "b,a")]
    [InlineData("entityType=Person&entityId=" + _person, "b,a")]
    [InlineData("entityType=Team&entityId=" + _person, "")]
    [InlineData("correlationId=elssc-req-0001", "c")]
    [InlineData("outcome=DENIED", "c")]
    [InlineData("from=2026-10-17T10:00:00Z&to=2026-10-17T12:00:00Z", "c,b")] // from inclusive, to exclusive
    [InlineData("from=2026-10-17T13:00:00%2B03:00&to=2026-10-17T12:00:00.000001Z", "d,c,b")]
    [InlineData("tenantId=" + _kmsa + "&outcome=SUCCESS", "a")]
    public async Task Filters_select_the_matching_entries(string query, string expected)
    {
        var entries = await SeedAsync();
        App.Authorization.Allow(PlatformActions.AuditRead);

        var page = await ListAsync("?" + query);

        page.Items.Select(e => e.Id).ShouldBe(expected.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(key => entries[key]));
        page.Page.HasMore.ShouldBeFalse();
        page.Page.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task Entry_shows_every_column_with_personal_data_masked()
    {
        var record = Record(_kmsa, _registrar, "person.update", "Person", _person, "kmsa-req-0042", AuditOutcome.Failure, 9) with
        {
            ErrorCode = "REGISTRY_PERSON_NOT_FOUND",
            Reason = "Guardian details corrected",
            Metadata = AuditMetadata.Serialize(new AuditTrailTests.PersonAuditMetadata(
                "Aisha Nakato", "aisha.nakato@example.ug", "CM9001234567XY", new(2011, 3, 14))),
        };
        await AppendAsync(record);
        App.Authorization.Allow(PlatformActions.AuditRead);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri($"{_listPath}?tenantId={_kmsa}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var entry = body.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("id").GetGuid().ShouldBe(record.Id);
        entry.GetProperty("occurredAt").GetDateTimeOffset().ShouldBe(record.OccurredAt);
        entry.GetProperty("actionCode").GetString().ShouldBe("person.update");
        entry.GetProperty("outcome").GetString().ShouldBe("FAILURE");
        entry.GetProperty("errorCode").GetString().ShouldBe("REGISTRY_PERSON_NOT_FOUND");
        entry.GetProperty("actorKind").GetString().ShouldBe("USER");
        entry.GetProperty("actorId").GetString().ShouldBe(_registrar);
        entry.GetProperty("tenantId").GetString().ShouldBe(_kmsa);
        entry.GetProperty("entityType").GetString().ShouldBe("Person");
        entry.GetProperty("entityId").GetString().ShouldBe(_person);
        entry.GetProperty("source").GetString().ShouldBe("API");
        entry.GetProperty("correlationId").GetString().ShouldBe("kmsa-req-0042");
        entry.GetProperty("reason").GetString().ShouldBe("Guardian details corrected");
        var metadata = entry.GetProperty("metadata");
        metadata.GetProperty("displayName").GetString().ShouldBe("Aisha Nakato");
        metadata.GetProperty("email").GetString().ShouldBe(AuditMetadata.Redacted);
        metadata.GetProperty("nationalId").GetString().ShouldBe(AuditMetadata.Redacted);
        metadata.GetProperty("dateOfBirth").GetString().ShouldBe(AuditMetadata.Redacted);
    }

    [Fact]
    public async Task Pages_newest_first_without_skipping_or_repeating_entries_at_the_same_instant()
    {
        foreach (var hour in new[] { 9, 10, 10, 10, 11 })
        {
            await AppendAsync(Record(_kmsa, _registrar, "person.update", "Person", _person, null, AuditOutcome.Success, hour));
        }

        App.Authorization.Allow(PlatformActions.AuditRead);
        var all = (await ListAsync($"?tenantId={_kmsa}")).Items;

        var pages = new List<ListAuditEntriesResponse> { await ListAsync($"?tenantId={_kmsa}&limit=2") };
        while (pages[^1].Page.NextCursor is { } cursor)
        {
            pages.Add(await ListAsync($"?tenantId={_kmsa}&limit=2&cursor={cursor}"));
        }

        pages.Select(p => p.Items.Count).ShouldBe([2, 2, 1]);
        pages.Select(p => p.Page.HasMore).ShouldBe([true, true, false]);
        pages.SelectMany(p => p.Items).Select(e => e.Id).ShouldBe(all.Select(e => e.Id));
        all.Select(e => e.Id).ShouldBeUnique();
        all.Select(e => e.OccurredAt).ShouldBeInOrder(SortDirection.Descending);
    }

    [Theory]
    [InlineData("?tenantId=kmsa", "tenantId")]
    [InlineData("?actorId=0192a1b20000700080000000000a011", "actorId")] // not the hyphenated form
    [InlineData("?action=Person.Update", "action")]
    [InlineData("?action=person", "action")]
    [InlineData("?entityType=", "entityType")]
    [InlineData("?entityId=" + _person, "entityId")] // needs entityType
    [InlineData("?entityType=Person&entityId=e001", "entityId")]
    [InlineData("?correlationId=", "correlationId")]
    [InlineData("?outcome=denied", "outcome")]
    [InlineData("?outcome=ERROR", "outcome")]
    [InlineData("?from=2026-10-17", "from")]
    [InlineData("?from=2026-10-17T09:00:00", "from")] // no offset
    [InlineData("?from=2026-10-17T09:00:00 03:00", "from")] // an unencoded + is a space
    [InlineData("?to=17/10/2026 09:00", "to")]
    [InlineData("?from=2026-10-17T10:00:00Z&to=2026-10-17T10:00:00Z", "to")]
    [InlineData("?from=2026-10-17T11:00:00Z&to=2026-10-17T10:00:00Z", "to")]
    [InlineData("?limit=0", "limit")]
    [InlineData("?limit=101", "limit")]
    [InlineData("?limit=abc", "limit")]
    [InlineData("?cursor=not-a-cursor", "cursor")]
    public async Task Invalid_query_is_rejected(string query, string field)
    {
        App.Authorization.Allow(PlatformActions.AuditRead);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri(_listPath + query, UriKind.Relative), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        problem.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
        var error = problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("field").GetString().ShouldBe(field);
        error.GetProperty("code").GetString().ShouldBe("VALIDATION_INVALID_QUERY");
    }

    [Fact]
    public async Task Every_invalid_parameter_is_listed()
    {
        App.Authorization.Allow(PlatformActions.AuditRead);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri($"{_listPath}?tenantId=kmsa&outcome=ERROR&limit=0", UriKind.Relative), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .ShouldBe(["tenantId", "outcome", "limit"]);
    }

    [Fact]
    public async Task List_is_denied_without_the_read_action_and_the_denial_is_audited()
    {
        App.Authorization.Allow(PlatformActions.OutboxRead);
        App.Actor.ActAsUser(_staffId);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri(_listPath, UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        App.Authorization.Requests.ShouldHaveSingleItem().Action.ShouldBe(PlatformActions.AuditRead);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("platform.audit.read"),
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.ActorId.ShouldBe(_staffId));
    }

    [Fact]
    public async Task List_is_allowed_with_the_read_action_and_audited_with_its_filters()
    {
        App.Authorization.Allow(PlatformActions.AuditRead);
        App.Actor.ActAsUser(_staffId);
        using var client = App.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"{_listPath}?tenantId={_kmsa}&outcome=DENIED&from=2026-10-17T09:00:00Z", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        App.Authorization.Requests.ShouldHaveSingleItem().Action.ShouldBe(PlatformActions.AuditRead);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("platform.audit.read"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.ActorId.ShouldBe(_staffId));
        var metadata = JsonDocument.Parse(entry.Metadata.ShouldNotBeNull()).RootElement;
        metadata.GetProperty("tenantId").GetString().ShouldBe(_kmsa);
        metadata.GetProperty("outcome").GetString().ShouldBe("DENIED");
        metadata.GetProperty("from").GetDateTimeOffset().ShouldBe(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));
        metadata.GetProperty("actorId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>The four entries of <see cref="Filters_select_the_matching_entries"/>, by letter.</summary>
    private async Task<Dictionary<string, Guid>> SeedAsync()
    {
        AuditRecord[] records =
        [
            Record(_kmsa, _registrar, "person.create", "Person", _person, "kmsa-req-0001", AuditOutcome.Success, 9),
            Record(_kmsa, _competitionManager, "person.update", "Person", _person, "kmsa-req-0002", AuditOutcome.Failure, 10),
            Record(_elssc, _registrar, "team.create", "Team", "0192a1b2-0000-7000-8000-00000000e002", "elssc-req-0001", AuditOutcome.Denied, 11),
            Record(null, null, "platform.outbox.redrive", "OutboxMessage", "0192a1b2-0000-7000-8000-00000000e003", null, AuditOutcome.Success, 12),
        ];
        foreach (var record in records)
        {
            await AppendAsync(record);
        }

        return records.Select((record, i) => (Key: ((char)('a' + i)).ToString(), record.Id)).ToDictionary(x => x.Key, x => x.Id);
    }

    private static AuditRecord Record(
        string? tenantId, string? actorId, string actionCode, string entityType, string entityId,
        string? correlationId, AuditOutcome outcome, int hour)
    {
        var occurredAt = new DateTimeOffset(2026, 10, 17, hour, 0, 0, TimeSpan.Zero);
        return new AuditRecord(
            Guid.CreateVersion7(occurredAt), actionCode, outcome, null,
            actorId is null ? ActorKind.System : ActorKind.User, actorId is null ? SystemActor.Id : Guid.Parse(actorId),
            tenantId is null ? null : Guid.Parse(tenantId), entityType, Guid.Parse(entityId), occurredAt, AuditSource.Api,
            correlationId, null, null);
    }

    private Task AppendAsync(AuditRecord record) => App.Services.GetRequiredService<IAuditLog>().AppendAsync(record, Ct);

    private async Task<ListAuditEntriesResponse> ListAsync(string query)
    {
        using var client = App.CreateClient();
        using var response = await client.GetAsync(new Uri(_listPath + query, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ListAuditEntriesResponse>(Json, Ct)).ShouldNotBeNull();
    }
}
