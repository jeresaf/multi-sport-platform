using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class AggregateRootTests
{
    // UUIDv7 orders by millisecond; IDs created in the same millisecond are random among themselves.
    [Fact]
    public void New_entities_get_unique_UUIDv7_ids_with_non_decreasing_timestamps()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => new Venue("Bukoto Community Stadium").Id).ToList();

        ids.ShouldAllBe(id => id.Version == 7);
        ids.Distinct().Count().ShouldBe(ids.Count);
        var timestamps = ids.Select(UnixMilliseconds).ToList();
        timestamps.ShouldBe(timestamps.Order(), ignoreOrder: false);
    }

    private static long UnixMilliseconds(Guid id) => Convert.ToInt64(id.ToString("N")[..12], 16);

    [Fact]
    public void An_entity_cannot_have_an_empty_id()
    {
        Should.Throw<ArgumentException>(() => new Venue(Guid.Empty, "Bukoto Community Stadium"));
    }

    [Fact]
    public void Raised_domain_events_are_kept_until_cleared()
    {
        var venue = new Venue("Bukoto Community Stadium");

        venue.Rename("Kisaasi Recreation Ground");
        venue.Rename("Seeta Hill Sports Field");

        venue.DomainEvents.Count.ShouldBe(2);
        venue.DomainEvents[1].ShouldBe(new VenueRenamed(venue.Id, "Seeta Hill Sports Field"));

        venue.ClearDomainEvents();

        venue.DomainEvents.ShouldBeEmpty();
    }

    private sealed record VenueRenamed(Guid VenueId, string Name) : IDomainEvent;

    private sealed class Venue : AggregateRoot
    {
        public Venue(string name) => Name = name;

        public Venue(Guid id, string name)
            : base(id) => Name = name;

        public string Name { get; private set; }

        public void Rename(string name)
        {
            Name = name;
            Raise(new VenueRenamed(Id, name));
        }
    }
}
