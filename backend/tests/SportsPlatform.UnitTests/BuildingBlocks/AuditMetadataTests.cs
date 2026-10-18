using System.Text.Json;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.UnitTests.BuildingBlocks;

/// <summary>AR-24 redaction: PRIVATE and SENSITIVE values never reach audit metadata.</summary>
public sealed class AuditMetadataTests
{
    [Fact]
    public void Private_and_sensitive_values_are_masked_and_the_rest_is_kept()
    {
        var json = AuditMetadata.Serialize(new GuardianChange(
            "Brian Ssemakula", "Brian Ssemakula Kato", "+256 772 123456", "CM8812345678AB", new(2010, 5, 2), 3)).ShouldNotBeNull();

        var metadata = JsonDocument.Parse(json).RootElement;
        metadata.GetProperty("displayName").GetString().ShouldBe("Brian Ssemakula");
        metadata.GetProperty("legalName").GetString().ShouldBe("Brian Ssemakula Kato");
        metadata.GetProperty("guardianPhone").GetString().ShouldBe(AuditMetadata.Redacted);
        metadata.GetProperty("nationalId").GetString().ShouldBe(AuditMetadata.Redacted);
        metadata.GetProperty("dateOfBirth").GetString().ShouldBe(AuditMetadata.Redacted);
        metadata.GetProperty("revision").GetInt32().ShouldBe(3);
        json.ShouldNotContain("772 123456");
        json.ShouldNotContain("CM8812345678AB");
        json.ShouldNotContain("2010");
    }

    [Fact]
    public void Nested_records_and_lists_are_masked_too()
    {
        var json = AuditMetadata.Serialize(new SquadChange("Ntinda Swifts FC", [new("Grace Atim", "grace.atim@example.ug")]));

        var player = JsonDocument.Parse(json!).RootElement.GetProperty("players")[0];
        player.GetProperty("displayName").GetString().ShouldBe("Grace Atim");
        player.GetProperty("email").GetString().ShouldBe(AuditMetadata.Redacted);
    }

    [Fact]
    public void Null_private_value_is_masked_as_well()
    {
        var json = AuditMetadata.Serialize(new GuardianChange("Brian Ssemakula", "Brian Ssemakula Kato", null, null, null, 1));

        JsonDocument.Parse(json!).RootElement.GetProperty("guardianPhone").GetString().ShouldBe(AuditMetadata.Redacted);
    }

    [Fact]
    public void No_metadata_is_null() => AuditMetadata.Serialize(null).ShouldBeNull();

    [Fact]
    public void Enums_are_upper_snake_strings() =>
        AuditMetadata.Serialize(new { Status = DayOfWeek.Saturday }).ShouldBe("""{"status":"SATURDAY"}""");

    public sealed record GuardianChange(
        [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
        [property: PersonalData(PersonalDataClass.Internal)] string LegalName,
        [property: PersonalData(PersonalDataClass.Private)] string? GuardianPhone,
        [property: PersonalData(PersonalDataClass.Sensitive)] string? NationalId,
        [property: PersonalData(PersonalDataClass.Private)] DateOnly? DateOfBirth,
        int Revision);

    public sealed record SquadChange(string Team, IReadOnlyList<SquadPlayer> Players);

    public sealed record SquadPlayer(
        [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
        [property: PersonalData(PersonalDataClass.Private)] string Email);
}
