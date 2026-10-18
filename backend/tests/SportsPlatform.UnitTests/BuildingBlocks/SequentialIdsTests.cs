using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class SequentialIdsTests
{
    [Fact]
    public void Ids_are_version_7_and_strictly_increasing()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => SequentialIds.Next()).ToList();

        ids.ShouldAllBe(id => id.Version == 7 && id.Variant >= 8 && id.Variant <= 11);
        for (var i = 1; i < ids.Count; i++)
        {
            ids[i].CompareTo(ids[i - 1]).ShouldBePositive();
        }
    }
}
