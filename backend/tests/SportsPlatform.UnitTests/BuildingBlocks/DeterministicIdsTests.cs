using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class DeterministicIdsTests
{
    [Fact]
    public void Matches_the_RFC_9562_version_5_example()
    {
        var dns = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        DeterministicIds.Create(dns, "www.example.com").ShouldBe(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"));
    }

    [Fact]
    public void The_same_key_always_gives_the_same_version_5_ID()
    {
        var id = DeterministicIds.Create("platform.country:UG");

        DeterministicIds.Create("platform.country:UG").ShouldBe(id);
        id.Version.ShouldBe(5);
        id.Variant.ShouldBeInRange(8, 11);
        // Pinned: the seed namespace and the algorithm must never change, or every seeded ID would.
        id.ShouldBe(new Guid("30952957-3cf5-5e72-bb32-2c46e97d3823"));
    }

    [Fact]
    public void Different_keys_give_different_IDs()
    {
        DeterministicIds.Create("platform.country:UG").ShouldNotBe(DeterministicIds.Create("platform.country:KE"));
    }
}
