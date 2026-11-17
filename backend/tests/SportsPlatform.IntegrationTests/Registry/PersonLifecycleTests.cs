using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The person lifecycle over HTTP (S06-T3a; D9): every status against every transition, with its history row and audit
/// entry; reasons, the version, the roles and the read-only tenant.
/// </summary>
public sealed class PersonLifecycleTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _reason = "Left the association";

    private static readonly Dictionary<string, (string Path, string Action)> _transitions = new()
    {
        ["Deactivate"] = ("deactivate", "person.deactivate"),
        ["Reactivate"] = ("reactivate", "person.reactivate"),
        ["Archive"] = ("archive", "person.archive"),
        ["Unarchive"] = ("unarchive", "person.unarchive"),
        ["MarkDeceased"] = ("mark-deceased", "person.mark_deceased"),
    };

    // D9: where each transition leads from each status; every other pair is refused.
    private static readonly Dictionary<(PersonStatus, PersonTransition), string> _allowed = new()
    {
        [(PersonStatus.Active, PersonTransition.Deactivate)] = "INACTIVE",
        [(PersonStatus.Inactive, PersonTransition.Reactivate)] = "ACTIVE",
        [(PersonStatus.Active, PersonTransition.Archive)] = "ARCHIVED",
        [(PersonStatus.Inactive, PersonTransition.Archive)] = "ARCHIVED",
        [(PersonStatus.Archived, PersonTransition.Unarchive)] = "ACTIVE",
        [(PersonStatus.Active, PersonTransition.MarkDeceased)] = "DECEASED",
        [(PersonStatus.Inactive, PersonTransition.MarkDeceased)] = "DECEASED",
    };

    public static TheoryData<PersonStatus, PersonTransition> EveryPair()
    {
        var data = new TheoryData<PersonStatus, PersonTransition>();
        foreach (var status in Enum.GetValues<PersonStatus>())
        {
            foreach (var transition in Enum.GetValues<PersonTransition>())
            {
                data.Add(status, transition);
            }
        }

        return data;
    }

    // T3 acceptance: every transition in D9 is allowed, every other refused, each with its history row and audit reason.
    [Theory]
    [MemberData(nameof(EveryPair))]
    public async Task Each_transition_is_allowed_only_from_its_states_with_its_history_row_and_audit_entry(
        PersonStatus from, PersonTransition transition)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await PersonInAsync(kmsa, from);
        var (path, action) = _transitions[transition.ToString()];
        var reason = PersonTransitions.NeedsReason(transition) ? $" {_reason} " : null;
        var fromCode = from.ToString().ToUpperInvariant();
        var before = (await registrar.ReadJsonAsync(StatusChangesPath(person), Ct)).GetProperty("items").GetArrayLength();

        using var response = reason is null
            ? await registrar.PostAsync(StatusPath(person, path), null, Ct)
            : await registrar.PostAsJsonAsync(StatusPath(person, path), new { reason }, Ct);

        var history = (await registrar.ReadJsonAsync(StatusChangesPath(person), Ct)).GetProperty("items").EnumerateArray().ToList();
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == action);
        (entry.EntityType, entry.EntityId, entry.Reason).ShouldBe(("Person", person, reason?.Trim()));
        if (_allowed.TryGetValue((from, transition), out var to))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            var answer = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            answer.GetProperty("status").GetString().ShouldBe(to);
            response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{answer.GetProperty("version").GetUInt32()}\"");
            history.Count.ShouldBe(before + 1);
            var row = history.Single(r => r.GetProperty("toStatus").GetString() == to
                && r.GetProperty("fromStatus").GetString() == fromCode
                && (r.GetProperty("reason").ValueKind == JsonValueKind.Null ? null : r.GetProperty("reason").GetString()) == reason?.Trim());
            row.GetProperty("actorKind").GetString().ShouldBe("USER");
            entry.Outcome.ShouldBe(AuditOutcome.Success);
        }
        else
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_STATUS_INVALID");
            history.Count.ShouldBe(before);
            (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("status").GetString().ShouldBe(fromCode);
            entry.ErrorCode.ShouldBe("PERSON_STATUS_INVALID");
        }
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("unarchive")]
    [InlineData("mark-deceased")]
    public async Task A_transition_that_needs_a_reason_is_refused_without_one(string path)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await PersonInAsync(kmsa, PersonStatus.Active);

        using var blank = await registrar.PostAsJsonAsync(StatusPath(person, path), new { reason = "  " }, Ct);
        using var tooLong = await registrar.PostAsJsonAsync(StatusPath(person, path), new { reason = new string('x', 501) }, Ct);
        using var noBody = await registrar.PostAsync(StatusPath(person, path), null, Ct);

        await blank.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        await tooLong.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        await noBody.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("status").GetString().ShouldBe("ACTIVE");
    }

    [Fact]
    public async Task Deactivating_needs_no_body_and_a_stale_version_conflicts()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await PersonInAsync(kmsa, PersonStatus.Active);
        var version = (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("version").GetUInt32();

        using (var stale = await registrar.PostAsJsonAsync(StatusPath(person, "deactivate"), new { version = version + 1 }, Ct))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
        }

        using (var staleTag = new HttpRequestMessage(HttpMethod.Post, StatusPath(person, "archive")) { Content = JsonContent.Create(new { reason = _reason }) })
        {
            staleTag.Headers.TryAddWithoutValidation("If-Match", $"\"{version + 1}\"");
            using var response = await registrar.SendAsync(staleTag, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
        }

        using var deactivated = await registrar.PostAsync(StatusPath(person, "deactivate"), null, Ct);
        deactivated.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivated.Content.ReadAsStringAsync(Ct));
        (await deactivated.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("status").GetString().ShouldBe("INACTIVE");
    }

    [Fact]
    public async Task Another_tenants_person_and_an_unknown_one_are_not_found()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, _) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var theirs = await PersonInAsync(elssc, PersonStatus.Active);

        foreach (var id in new[] { theirs, Guid.CreateVersion7() })
        {
            using var response = await registrar.PostAsJsonAsync(StatusPath(id, "archive"), new { reason = _reason }, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        }
    }

    // D21: the lifecycle is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_move_them_along_the_lifecycle(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var person = await PersonInAsync(kmsa, PersonStatus.Active);

        foreach (var (path, body) in new (string, object?)[]
        {
            ("deactivate", null), ("reactivate", null), ("archive", new { reason = _reason }), ("unarchive", new { reason = _reason }),
            ("mark-deceased", new { reason = _reason }),
        })
        {
            using var response = body is null
                ? await client.PostAsync(StatusPath(person, path), null, Ct)
                : await client.PostAsJsonAsync(StatusPath(person, path), body, Ct);
            if (allowed)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
            }
            else
            {
                await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
            }
        }
    }

    [Fact]
    public async Task A_read_only_tenant_moves_nobody()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);

        foreach (var path in _transitions.Values.Select(transition => transition.Path))
        {
            using var response = await registrar.PostAsJsonAsync(StatusPath(person.Id, path), new { reason = _reason }, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }
    }

    // A person created through the handlers and moved to a status through the domain.
    private async Task<Guid> PersonInAsync(Guid tenantId, PersonStatus status)
    {
        var person = await CreatePersonAsync(App.Services, tenantId, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        PersonTransition? transition = status switch
        {
            PersonStatus.Active => null,
            PersonStatus.Inactive => PersonTransition.Deactivate,
            PersonStatus.Archived => PersonTransition.Archive,
            PersonStatus.Deceased => PersonTransition.MarkDeceased,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        if (transition is { } t)
        {
            await ChangeStatusAsync(App.Services, tenantId, person, t, Ct);
        }

        return person;
    }

    /// <summary>An active tenant (time zone Africa/Kampala) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}
