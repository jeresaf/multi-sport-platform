using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.UnitTests.Registry;

// S06 D4: normalised names and the name key.
public sealed class NameNormaliserTests
{
    [Theory]
    [InlineData("Ssekandi", "ssekandi")]
    [InlineData("  NANSUBUGA  ", "nansubuga")]
    [InlineData("Nänsubuga", "nansubuga")]
    [InlineData("Akéllo", "akello")]
    [InlineData("Ng'ang'a", "nganga")]
    [InlineData("Ng’ang’a", "nganga")]
    [InlineData("Okello-Owino", "okelloowino")]
    [InlineData("Otim–Okot", "otimokot")]
    [InlineData("J.", "j")]
    [InlineData("St. John", "st john")]
    [InlineData("Mary   Anne\tGrace", "mary anne grace")]
    public void A_name_part_is_trimmed_lower_cased_and_stripped_of_accents_and_punctuation(string name, string expected) =>
        NameNormaliser.Normalise(name).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-'.")]
    public void A_name_without_letters_or_digits_normalises_to_nothing(string? name) =>
        NameNormaliser.Normalise(name).ShouldBeEmpty();

    [Fact]
    public void Nansubuga_with_and_without_an_accent_normalise_alike() =>
        NameNormaliser.Normalise("Nänsubuga").ShouldBe(NameNormaliser.Normalise("Nansubuga"));

    [Fact]
    public void A_decomposed_accent_normalises_like_a_composed_one() =>
        NameNormaliser.Normalise("Nansubugá").ShouldBe(NameNormaliser.Normalise("Nansubugá"));

    [Fact]
    public void The_name_key_does_not_depend_on_the_order_of_the_names()
    {
        var entered = NameNormaliser.NameKey("Ssekandi", null, "John", null);
        var existing = NameNormaliser.NameKey("John", null, "Ssekandi", null);

        entered.ShouldBe("john ssekandi");
        existing.ShouldBe(entered);
    }

    [Theory]
    // Luganda: twins' names and a clan name in other names.
    [InlineData("Babirye", "Lillian", "Namaganda", "Nkima", "babirye lillian namaganda nkima")]
    // Luo: an apostrophe-free compound and a hyphenated surname.
    [InlineData("Denis", null, "Opoka-Otim", null, "denis opokaotim")]
    // English: a middle initial and extra spaces.
    [InlineData(" Peter ", "J.", "Ochieng  Okot", null, "j ochieng okot peter")]
    public void The_name_key_is_the_sorted_tokens_of_every_part(
        string first, string? middle, string last, string? other, string expected) =>
        NameNormaliser.NameKey(first, middle, last, other).ShouldBe(expected);

    [Fact]
    public void A_name_entered_in_one_field_has_the_key_of_the_same_name_in_two() =>
        NameNormaliser.NameKey("Peter Ochieng Okot").ShouldBe(NameNormaliser.NameKey("Peter", "Ochieng", "Okot"));

    [Fact]
    public void Blank_parts_give_an_empty_key() =>
        NameNormaliser.NameKey(null, " ", "").ShouldBeEmpty();
}
