using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Host;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.IntegrationTests.Messaging;

namespace SportsPlatform.IntegrationTests.Catalogues;

public sealed class EventCatalogueTests
{
    private static readonly EventCatalogueSource _fixtures =
        new("Fixtures", [typeof(FixtureRescheduled.V2), typeof(FixtureRescheduled.V1), typeof(FixturePublished.V1)]);

    // What the CLI exports: the Host's modules and the consumers they register. No module declares an event yet, so the
    // value is that the build succeeds: every module's Contracts assembly loads and no consumer subscribes to an
    // event no Contracts declare.
    [Fact]
    public void The_Host_catalogue_builds_from_its_modules_and_consumers()
    {
        var services = new ServiceCollection()
            .AddSportsPlatform(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        var catalogue = EventCatalogue.Build(ModuleRegistry.All, services.GetServices<IntegrationEventSubscription>());

        catalogue.Select(entry => entry.Module).ShouldAllBe(module => ModuleRegistry.All.Any(m => m.Name == module));
    }

    [Fact]
    public void Events_are_listed_by_name_and_version_with_producer_and_sorted_consumers()
    {
        var catalogue = EventCatalogue.Build([_fixtures], Subscriptions(
            services => services
                .AddIntegrationEventConsumer<FixtureRescheduled.V1, NoopConsumer<FixtureRescheduled.V1>, MessagingTestDbContext>("progression.standings")
                .AddIntegrationEventConsumer<FixtureRescheduled.V1, NoopConsumer<FixtureRescheduled.V1>, MessagingTestDbContext>("engagement.notifications")));

        catalogue.Select(entry => entry.Event.ToString()).ShouldBe(["FixturePublished.V1", "FixtureRescheduled.V1", "FixtureRescheduled.V2"]);
        catalogue.ShouldAllBe(entry => entry.Module == "Fixtures");
        catalogue[1].Consumers.ShouldBe(["engagement.notifications", "progression.standings"]);
        catalogue[0].Consumers.ShouldBeEmpty();
        catalogue[2].Consumers.ShouldBeEmpty();
    }

    [Fact]
    public void Payload_lists_the_serialized_members_with_type_and_nullability()
    {
        var catalogue = EventCatalogue.Build([_fixtures], []);

        catalogue.Single(entry => entry.Event == new IntegrationEventName("FixtureRescheduled", 1)).Payload.ShouldBe(
        [
            new EventPayloadField("fixtureId", "uuid", false),
            new EventPayloadField("startsAt", "instant", false),
            new EventPayloadField("round", "integer", true),
            new EventPayloadField("venueName", "string", false),
            new EventPayloadField("note", "string", true),
            new EventPayloadField("officialIds", "uuid[]", false),
            new EventPayloadField("matchDate", "date", false),
            new EventPayloadField("kind", "string (enum FixtureKind: LEAGUE | ROUND_ROBIN)", false),
        ]);
        catalogue.Single(entry => entry.Event == new IntegrationEventName("FixturePublished", 1)).Payload.ShouldBeEmpty();
    }

    [Fact]
    public void An_event_declared_twice_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            EventCatalogue.Build([_fixtures, new EventCatalogueSource("Scheduling", [typeof(FixturePublished.V1)])], []));

        exception.Message.ShouldContain("FixturePublished.V1");
    }

    [Fact]
    public void A_consumer_of_an_undeclared_event_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() => EventCatalogue.Build([_fixtures], Subscriptions(
            services => services.AddIntegrationEventConsumer<ProbeCreated.V1, NoopConsumer<ProbeCreated.V1>, MessagingTestDbContext>("progression.probes"))));

        exception.Message.ShouldContain("progression.probes → ProbeCreated.V1");
    }

    [Fact]
    public void A_consumer_subscribed_twice_to_an_event_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() => EventCatalogue.Build([_fixtures], Subscriptions(
            services => services
                .AddIntegrationEventConsumer<FixturePublished.V1, NoopConsumer<FixturePublished.V1>, MessagingTestDbContext>("progression.standings")
                .AddIntegrationEventConsumer<FixturePublished.V1, NoopConsumer<FixturePublished.V1>, MessagingTestDbContext>("progression.standings"))));

        exception.Message.ShouldContain("progression.standings → FixturePublished.V1");
    }

    [Fact]
    public void A_struct_event_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            EventCatalogue.Build([new EventCatalogueSource("Fixtures", [typeof(FixtureMoved.V1)])], []));

        exception.Message.ShouldContain("must be record classes");
    }

    // A struct event in a Contracts assembly is found (and then rejected by Build), not silently skipped.
    [Fact]
    public void Struct_events_are_discovered()
    {
        EventCatalogue.EventTypes(typeof(EventCatalogueTests).Assembly).ShouldContain(typeof(FixtureMoved.V1));
    }

    [Theory]
    [InlineData(typeof(FixtureScored.V1), "scores")]
    [InlineData(typeof(FixtureScored.V2), "sheet")]
    [InlineData(typeof(FixtureScored.V3), "venue")]
    [InlineData(typeof(FixtureScored.V4), "rounds")]
    [InlineData(typeof(FixtureScored.V5), "kickOff")]
    public void A_member_the_catalogue_cannot_describe_is_rejected(Type eventType, string member)
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            EventCatalogue.Build([new EventCatalogueSource("Fixtures", [eventType])], []));

        exception.Message.ShouldContain($"member '{member}'");
    }

    [Fact]
    public void A_badly_named_event_is_rejected()
    {
        Should.Throw<ArgumentException>(() => EventCatalogue.Build([new EventCatalogueSource("Fixtures", [typeof(UnversionedFixtureEvent)])], []));
    }

    [Fact]
    public void Markdown_and_JSON_carry_every_field()
    {
        var catalogue = EventCatalogue.Build([_fixtures], Subscriptions(
            services => services.AddIntegrationEventConsumer<FixtureRescheduled.V2, NoopConsumer<FixtureRescheduled.V2>, MessagingTestDbContext>("progression.standings")));

        var markdown = EventCatalogue.ToMarkdown(catalogue);
        var json = EventCatalogue.ToJson(catalogue);

        markdown.ShouldContain("| `FixturePublished` | 1 | Fixtures | — | — |\n");
        markdown.ShouldContain("| `FixtureRescheduled` | 2 | Fixtures | `fixtureId: uuid`, `startsAt: instant`, `note: string?` | `progression.standings` |\n");
        markdown.ShouldContain(" `kind: string (enum FixtureKind: LEAGUE \\| ROUND_ROBIN)` | — |\n");
        markdown.ShouldNotContain("\r");
        json.ShouldNotContain("\r");
        json.ShouldEndWith("}\n");
        using var document = JsonDocument.Parse(json);
        var events = document.RootElement.GetProperty("events").EnumerateArray().ToList();
        events.Count.ShouldBe(3);
        var v2 = events[2];
        v2.GetProperty("name").GetString().ShouldBe("FixtureRescheduled");
        v2.GetProperty("version").GetInt32().ShouldBe(2);
        v2.GetProperty("module").GetString().ShouldBe("Fixtures");
        var note = v2.GetProperty("payload")[2];
        note.GetProperty("name").GetString().ShouldBe("note");
        note.GetProperty("type").GetString().ShouldBe("string");
        note.GetProperty("nullable").GetBoolean().ShouldBeTrue();
        v2.GetProperty("consumers").EnumerateArray().Select(consumer => consumer.GetString()).ShouldBe(["progression.standings"]);
    }

    [Fact]
    public void An_empty_catalogue_says_so()
    {
        EventCatalogue.ToMarkdown([]).ShouldEndWith("No integration events are declared yet.\n");
        EventCatalogue.ToJson([]).ShouldBe("{\n  \"events\": []\n}\n");
    }

    // The exporter only reads Contracts assemblies; an event declared anywhere else in the product would be missed.
    [Fact]
    public void Integration_events_are_declared_only_in_Contracts()
    {
        IEnumerable<Assembly> elsewhere =
        [
            typeof(IIntegrationEvent).Assembly,
            typeof(IntegrationEventSubscription).Assembly,
            typeof(EventCatalogue).Assembly,
            .. ModuleRegistry.All.Select(module => module.GetType().Assembly),
        ];

        elsewhere.SelectMany(EventCatalogue.EventTypes).Select(type => type.FullName).ShouldBeEmpty();
    }

    private static List<IntegrationEventSubscription> Subscriptions(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return [.. services.BuildServiceProvider().GetServices<IntegrationEventSubscription>()];
    }

    public enum FixtureKind
    {
        League,
        RoundRobin,
    }

    public static class FixtureRescheduled
    {
        public sealed record V1(
            Guid FixtureId, DateTimeOffset StartsAt, int? Round, string VenueName, string? Note, IReadOnlyList<Guid> OfficialIds,
            DateOnly MatchDate, FixtureKind Kind) : IIntegrationEvent;

        public sealed record V2(Guid FixtureId, DateTimeOffset StartsAt, string? Note) : IIntegrationEvent;
    }

    public static class FixturePublished
    {
        public sealed record V1 : IIntegrationEvent;
    }

    public sealed record UnversionedFixtureEvent : IIntegrationEvent;

    public static class FixtureMoved
    {
        public readonly record struct V1(Guid FixtureId) : IIntegrationEvent;
    }

    public sealed record VenueSummary(Guid VenueId, string Name);

    /// <summary>Members serialized as objects, base64, nested lists or zone-less times, which the catalogue doesn't describe.</summary>
    public static class FixtureScored
    {
        public sealed record V1(IReadOnlyDictionary<string, int> Scores) : IIntegrationEvent;

        public sealed record V2(byte[] Sheet) : IIntegrationEvent;

        public sealed record V3(VenueSummary Venue) : IIntegrationEvent;

        public sealed record V4(IReadOnlyList<IReadOnlyList<int>> Rounds) : IIntegrationEvent;

        public sealed record V5(DateTime KickOff) : IIntegrationEvent;
    }

    private sealed class NoopConsumer<TEvent> : IIntegrationEventConsumer<TEvent>
        where TEvent : IIntegrationEvent
    {
        public Task HandleAsync(TEvent integrationEvent, IntegrationEventEnvelope envelope, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
