using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>The duplicate check's token (S06 D6): what it binds, and how long it lives.</summary>
public sealed class DuplicateCheckTokensTests
{
    private static readonly Guid _kmsa = new("0192a1b2-0000-7000-8000-00000000c001");
    private static readonly Guid _elssc = new("0192a1b2-0000-7000-8000-00000000c002");
    private static readonly Guid _brian = new("0192a1b2-0000-7000-8000-0000000000b1");
    private static readonly Guid _grace = new("0192a1b2-0000-7000-8000-0000000000b2");
    private static readonly Guid _john = new("0192a1b2-0000-7000-8000-0000000000a1");

    private static readonly CheckDuplicatesRequest _shamim = new(
        "Shamim", "Nankya", DateOfBirth: new DateOnly(2004, 9, 21), Nationality: "UG",
        ExternalIdentifiers: [new("KMSA_REG_NO", "KMSA-00388"), new("SCHOOL_ADMISSION_NO", "EL/2019/045")]);

    private readonly EphemeralDataProtectionProvider _dataProtection = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Requests_the_check_cant_tell_apart_have_the_same_hash()
    {
        // Case, accents and spaces don't change a name key; the last name stays the last name (the sibling rule reads it).
        var reordered = _shamim with
        {
            FirstName = " SHAMÍM ",
            LastName = "Nan'kya",
            ExternalIdentifiers = [new("SCHOOL_ADMISSION_NO", "EL/2019/045"), new("KMSA_REG_NO", " KMSA-00388 ")],
        };

        Hash(reordered).ShouldBe(Hash(_shamim));
    }

    [Theory]
    [InlineData("date of birth")]
    [InlineData("nationality")]
    [InlineData("secondary nationality")]
    [InlineData("identifier")]
    [InlineData("the names swapped (another last name)")]
    public void Any_other_difference_changes_the_hash(string difference)
    {
        var other = difference switch
        {
            "date of birth" => _shamim with { DateOfBirth = new DateOnly(2004, 9, 22) },
            "nationality" => _shamim with { Nationality = "KE" },
            "secondary nationality" => _shamim with { SecondaryNationality = "KE" },
            "identifier" => _shamim with { ExternalIdentifiers = [new("KMSA_REG_NO", "KMSA-00388")] },
            _ => _shamim with { FirstName = "Nankya", LastName = "Shamim" },
        };

        Hash(other).ShouldNotBe(Hash(_shamim));
        DuplicateCheckTokens.Hash(PersonDetailsRules.ToCheckInput(_shamim, _john)).ShouldNotBe(Hash(_shamim));
    }

    [Fact]
    public void A_token_answers_its_candidates_to_the_same_actor_tenant_and_details_for_thirty_minutes()
    {
        var input = PersonDetailsRules.ToCheckInput(_shamim, null);
        var token = Tokens(_kmsa, _brian).Issue(input, [_john, _john]);

        _clock.Advance(TimeSpan.FromMinutes(30) - TimeSpan.FromMilliseconds(1));
        Tokens(_kmsa, _brian).Read(token, input).ShouldBe([_john]);
        Tokens(_kmsa, _grace).Read(token, input).ShouldBeNull();
        Tokens(_elssc, _brian).Read(token, input).ShouldBeNull();
        Tokens(_kmsa, _brian).Read(token, input with { Nationality = "KE" }).ShouldBeNull();
        Tokens(_kmsa, _brian).Read(token + "x", input).ShouldBeNull();
        Tokens(_kmsa, _brian).Read("", input).ShouldBeNull();

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Tokens(_kmsa, _brian).Read(token, input).ShouldBeNull();
    }

    [Fact]
    public void Another_purposes_protected_payload_is_not_a_token()
    {
        var input = PersonDetailsRules.ToCheckInput(_shamim, null);
        var foreign = _dataProtection.CreateProtector("platform.media").Protect("{}");

        Tokens(_kmsa, _brian).Read(foreign, input).ShouldBeNull();
    }

    private static string Hash(CheckDuplicatesRequest request) =>
        DuplicateCheckTokens.Hash(PersonDetailsRules.ToCheckInput(request, null));

    private DuplicateCheckTokens Tokens(Guid tenantId, Guid userId)
    {
        var tenant = new TenantContext();
        tenant.Set(tenantId);
        return new DuplicateCheckTokens(_dataProtection, _clock, tenant, new UserActor(userId));
    }
}
