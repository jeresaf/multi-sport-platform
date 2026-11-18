using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;

namespace SportsPlatform.UnitTests.Platform.Media;

public sealed class MediaAssetTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_asset_is_active_private_and_counts_every_file()
    {
        var asset = Photo();

        asset.Status.ShouldBe(MediaAssetStatus.Active);
        asset.Visibility.ShouldBe(MediaVisibility.Private);
        asset.SizeBytes.ShouldBe(1_000 + 20_000 + 90_000);
        asset.FileOf(MediaVariant.Medium)!.Key.ShouldBe("t/a/medium.webp");
        asset.FileOf(MediaVariant.Original).ShouldBeNull();
    }

    [Fact]
    public void Removing_is_final_and_a_second_removal_changes_nothing()
    {
        var asset = Photo();
        var registrar = new UserActor(Guid.NewGuid());

        asset.Remove(registrar, _now);
        asset.Remove(SystemActor.Instance, _now.AddHours(1));

        asset.Status.ShouldBe(MediaAssetStatus.Removed);
        (asset.RemovedByKind, asset.RemovedById, asset.RemovedAt).ShouldBe((ActorKind.User, registrar.ActorId, _now));
    }

    [Fact]
    public void An_asset_has_one_file_per_variant() =>
        Should.Throw<ArgumentException>(() => MediaAsset.Create(
            MediaAsset.NewAssetId(), MediaPurpose.PersonPhoto, "image/webp", "t/a",
            [File(MediaVariant.Large, 1), File(MediaVariant.Large, 2)], SystemActor.Instance, _now));

    [Theory]
    [InlineData(0L, 0)]
    [InlineData((1L << 30) - 1, 0)]
    [InlineData(1L << 30, 1)]
    [InlineData((10L << 30) + 5, 10)]
    public void Storage_counts_whole_gigabytes_rounded_down(long bytes, int gigabytes) =>
        MediaStorageUsage.ToWholeGigabytes(bytes).ShouldBe(gigabytes);

    private static MediaAsset Photo() => MediaAsset.Create(
        MediaAsset.NewAssetId(), MediaPurpose.PersonPhoto, "image/webp", "t/a",
        [File(MediaVariant.Thumbnail, 1_000), File(MediaVariant.Medium, 20_000), File(MediaVariant.Large, 90_000)],
        SystemActor.Instance, _now);

    private static MediaAssetFile File(MediaVariant variant, long bytes) =>
        new(variant, $"t/a/{variant.ToString().ToLowerInvariant()}.webp", "image/webp", 10, 10, bytes);
}
