using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Platform.Domain.Reference;

namespace SportsPlatform.UnitTests.Platform;

public sealed class ReferenceEntryTests
{
    [Fact]
    public void A_new_entry_is_active_with_the_ID_from_its_code()
    {
        var country = new Country("UG", "UGA", "800", "Uganda");

        country.IsActive.ShouldBeTrue();
        country.Id.ShouldBe(DeterministicIds.Create("platform.country:UG"));
        new Currency("UGX", "800", "Ugandan Shilling", 0).Id.ShouldBe(DeterministicIds.Create("platform.currency:UGX"));
        new Language("sw", "Swahili").Id.ShouldBe(DeterministicIds.Create("platform.language:sw"));
        new IanaTimeZone("Africa/Kampala", "UG").Id.ShouldBe(DeterministicIds.Create("platform.time_zone:Africa/Kampala"));
    }

    [Fact]
    public void Applying_the_same_values_changes_nothing()
    {
        var currency = new Currency("UGX", "800", "Ugandan Shilling", 0);

        currency.Apply("800", "Ugandan Shilling", 0).ShouldBeFalse();
        currency.Apply("800", "Ugandan Shilling", null).ShouldBeTrue();
        currency.MinorUnits.ShouldBeNull();
    }

    [Fact]
    public void A_deactivated_entry_is_reactivated_when_the_data_lists_it_again()
    {
        var language = new Language("lg", "Ganda");

        language.Deactivate().ShouldBeTrue();
        language.Deactivate().ShouldBeFalse();
        language.IsActive.ShouldBeFalse();

        language.Apply("Ganda").ShouldBeTrue();
        language.IsActive.ShouldBeTrue();
    }
}
