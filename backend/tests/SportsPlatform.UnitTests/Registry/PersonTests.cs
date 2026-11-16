using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.UnitTests.Registry;

// S06 D2, D4, D9, D10: the person as created, its keys and its children.
public sealed class PersonTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_person_is_active_with_its_keys_and_the_first_row_of_its_history()
    {
        var person = Create(new PersonBiodata("  John ", "Ssekandi", DateOfBirth: new DateOnly(2001, 4, 12), Nationality: "UG"));

        person.FirstName.ShouldBe("John");
        person.LastName.ShouldBe("Ssekandi");
        person.DisplayName.ShouldBe("John Ssekandi");
        person.FirstNameKey.ShouldBe("john");
        person.LastNameKey.ShouldBe("ssekandi");
        person.NameKey.ShouldBe("john ssekandi");
        person.Status.ShouldBe(PersonStatus.Active);
        person.Source.ShouldBe(PersonSource.Manual);
        person.PublicProfileEnabled.ShouldBeFalse();
        person.PhotoAssetId.ShouldBeNull();
        person.TenantId.ShouldBe(Guid.Empty);

        var first = person.StatusHistory.ShouldHaveSingleItem();
        first.PersonId.ShouldBe(person.Id);
        first.FromStatus.ShouldBeNull();
        first.ToStatus.ShouldBe(PersonStatus.Active);
        first.ActorKind.ShouldBe(ActorKind.System);
        first.OccurredAt.ShouldBe(_now);
    }

    [Fact]
    public void The_name_key_takes_the_middle_and_other_names_and_ignores_their_order()
    {
        var entered = Create(new PersonBiodata("Ssekandi", "John", MiddleName: "Paul", OtherNames: "Mmamba"));
        var existing = Create(new PersonBiodata("John", "Ssekandi", MiddleName: "Paul", OtherNames: "Mmamba"));

        entered.NameKey.ShouldBe("john mmamba paul ssekandi");
        existing.NameKey.ShouldBe(entered.NameKey);
    }

    [Fact]
    public void A_display_name_is_kept_when_given()
    {
        var person = Create(new PersonBiodata("Babirye Lillian", "Namaganda", DisplayName: " Lillian  Namaganda "));

        person.DisplayName.ShouldBe("Lillian Namaganda");
    }

    [Theory]
    [InlineData("", "Akello")]
    [InlineData("Brenda", "  ")]
    [InlineData("-", "Akello")]
    public void First_and_last_name_are_required(string first, string last) =>
        Should.Throw<ArgumentException>(() => Create(new PersonBiodata(first, last)));

    [Theory]
    [InlineData("ug")]
    [InlineData("UGA")]
    [InlineData("U1")]
    public void A_nationality_is_an_alpha_2_code(string nationality) =>
        Should.Throw<ArgumentException>(() => Create(new PersonBiodata("Brenda", "Akello", Nationality: nationality)));

    [Theory]
    [InlineData(49)]
    [InlineData(261)]
    public void Height_is_bounded(int height) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(new PersonBiodata("Denis", "Opoka", HeightCm: height)));

    [Theory]
    [InlineData(9.9)]
    [InlineData(300.1)]
    public void Weight_is_bounded(double weight) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(new PersonBiodata("Denis", "Opoka", WeightKg: (decimal)weight)));

    [Fact]
    public void The_bounds_themselves_are_accepted()
    {
        var person = Create(new PersonBiodata(
            "Denis", "Opoka", HeightCm: PersonBiodata.MaxHeightCm, WeightKg: PersonBiodata.MinWeightKg));

        person.HeightCm.ShouldBe(260);
        person.WeightKg.ShouldBe(10m);
    }

    [Fact]
    public void An_alias_has_a_name_key_of_its_own()
    {
        var person = Create(new PersonBiodata("Peter", "Ochieng"));

        var alias = person.AddAlias("Okot  Peter", AliasKind.FormerName);

        person.Aliases.ShouldHaveSingleItem().ShouldBeSameAs(alias);
        alias.PersonId.ShouldBe(person.Id);
        alias.Name.ShouldBe("Okot Peter");
        alias.NameKey.ShouldBe("okot peter");
        alias.Kind.ShouldBe(AliasKind.FormerName);
        alias.IsRemoved.ShouldBeFalse();
        person.NameKey.ShouldBe("ochieng peter");
    }

    [Fact]
    public void An_external_identifier_keeps_its_authority_and_trimmed_value()
    {
        var person = Create(new PersonBiodata("Mariam", "Nankya"));

        var identifier = ExternalIdentifier.ForPerson(person.Id, "KMSA_REG_NO", "  KM-2026-0147 ", new DateOnly(2026, 1, 10));

        identifier.EntityType.ShouldBe(ExternalIdentifierEntityType.Person);
        identifier.EntityId.ShouldBe(person.Id);
        identifier.Authority.ShouldBe("KMSA_REG_NO");
        identifier.Value.ShouldBe("KM-2026-0147");
        identifier.ValidFrom.ShouldBe(new DateOnly(2026, 1, 10));
        identifier.ValidTo.ShouldBeNull();
    }

    [Theory]
    [InlineData("kmsa_reg_no")]
    [InlineData("K")]
    [InlineData("1KMSA")]
    [InlineData("KMSA REG")]
    public void An_authority_is_an_upper_snake_code(string authority) =>
        Should.Throw<ArgumentException>(() => ExternalIdentifier.ForPerson(Guid.CreateVersion7(), authority, "KM-2026-0147"));

    [Fact]
    public void An_identifier_needs_a_value() =>
        Should.Throw<ArgumentException>(() => ExternalIdentifier.ForPerson(Guid.CreateVersion7(), "KMSA_REG_NO", "  "));

    private static Person Create(PersonBiodata biodata) =>
        Person.Create(biodata, PersonSource.Manual, SystemActor.Instance, _now);
}
