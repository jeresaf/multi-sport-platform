using System.Text.RegularExpressions;
using SportsPlatform.Modules.Platform.Seed.Reference;

namespace SportsPlatform.UnitTests.Platform;

/// <summary>The embedded reference data files: well-formed, unique, and holding what the pilot customer needs.</summary>
public sealed partial class ReferenceDataFilesTests
{
    private static readonly ReferenceDataFiles _files = new();
    private static readonly string[] _eastAfrica = ["KE", "TZ", "RW", "BI", "SS", "CD"];
    private static readonly string[] _pilotLanguages = ["en", "fr", "lg", "rw"];
    private static readonly string[] _currencyCodesUsedInEastAndCentralAfrica = ["UGX", "KES", "TZS", "RWF", "BIF", "SSP", "CDF", "XAF", "USD"];
    private static readonly string[] _notMoney = ["BOV", "USN", "XAD", "XAU", "XAG", "XBA", "XDR", "XSU", "XUA", "XTS", "XXX"];

    private static IReadOnlyList<CountryItem> Countries => _files.Items<CountryItem>("countries.json");

    private static IReadOnlyList<CurrencyItem> Currencies => _files.Items<CurrencyItem>("currencies.json");

    private static IReadOnlyList<LanguageItem> Languages => _files.Items<LanguageItem>("languages.json");

    private static IReadOnlyList<TimeZoneItem> TimeZones => _files.Items<TimeZoneItem>("time-zones.json");

    [Fact]
    public void Countries_are_the_ISO_3166_1_codes_with_unique_alpha3_and_numeric_codes()
    {
        Countries.Count.ShouldBe(249);
        Countries.ShouldAllBe(c => UpperAlpha2().IsMatch(c.Code) && UpperAlpha3().IsMatch(c.Alpha3Code) && Numeric3().IsMatch(c.NumericCode));
        Countries.ShouldAllBe(c => !string.IsNullOrWhiteSpace(c.Name));
        ShouldBeUnique(Countries.Select(c => c.Code));
        ShouldBeUnique(Countries.Select(c => c.Alpha3Code));
        ShouldBeUnique(Countries.Select(c => c.NumericCode));
        Countries.ShouldNotContain(c => c.Code == "XK"); // User-assigned, not ISO 3166-1.
        Countries.Single(c => c.Code == "UG").ShouldBe(new CountryItem("UG", "UGA", "800", "Uganda"));
        // Display names, not CLDR's disambiguating defaults ("Congo - Kinshasa").
        Countries.Single(c => c.Code == "CD").Name.ShouldBe("Congo (DRC)");
        Countries.Single(c => c.Code == "CG").Name.ShouldBe("Congo (Republic)");
        Countries.Single(c => c.Code == "GB").Name.ShouldBe("United Kingdom"); // Not the short "UK".
        _eastAfrica.ShouldBeSubsetOf(Countries.Select(c => c.Code));
    }

    [Fact]
    public void Currencies_are_unique_ISO_4217_codes_of_money_with_their_minor_units()
    {
        Currencies.ShouldAllBe(c => UpperAlpha3().IsMatch(c.Code) && Numeric3().IsMatch(c.NumericCode) && !string.IsNullOrWhiteSpace(c.Name));
        Currencies.ShouldAllBe(c => c.MinorUnits >= 0 && c.MinorUnits <= 6);
        ShouldBeUnique(Currencies.Select(c => c.Code));
        ShouldBeUnique(Currencies.Select(c => c.NumericCode));
        Currencies.Single(c => c.Code == "UGX").ShouldBe(new CurrencyItem("UGX", "800", "Ugandan Shilling", 0));
        Currencies.Single(c => c.Code == "KES").MinorUnits.ShouldBe(2);
        _currencyCodesUsedInEastAndCentralAfrica.ShouldBeSubsetOf(Currencies.Select(c => c.Code));
        _notMoney.ShouldAllBe(code => !Currencies.Any(c => c.Code == code)); // Funds, metals, units, testing, none.
    }

    [Fact]
    public void Languages_are_every_ISO_639_1_code()
    {
        Languages.Count.ShouldBe(183);
        Languages.ShouldAllBe(l => LowerAlpha2().IsMatch(l.Code) && !string.IsNullOrWhiteSpace(l.Name));
        ShouldBeUnique(Languages.Select(l => l.Code));
        Languages.Single(l => l.Code == "sw").Name.ShouldBe("Swahili");
        _pilotLanguages.ShouldBeSubsetOf(Languages.Select(l => l.Code));
    }

    [Fact]
    public void Time_zones_are_unique_IANA_IDs_whose_countries_exist()
    {
        ShouldBeUnique(TimeZones.Select(z => z.Code));
        TimeZones.ShouldAllBe(z => z.Code.Length <= 64 && z.Code.Contains('/'));
        TimeZones.Where(z => z.CountryCode is not null).Select(z => z.CountryCode!).Distinct()
            .ShouldBeSubsetOf(Countries.Select(c => c.Code));
        TimeZones.Single(z => z.Code == "Africa/Kampala").CountryCode.ShouldBe("UG"); // Its own zone, not Africa/Nairobi.
        TimeZones.Single(z => z.CountryCode is null).Code.ShouldBe("Etc/UTC");
    }

    [Fact]
    public void Every_country_has_a_time_zone_except_the_uninhabited_ones_tzdata_leaves_out() =>
        Countries.Select(c => c.Code).Except(TimeZones.Select(z => z.CountryCode).OfType<string>())
            .ShouldBe(["BV", "HM"], ignoreOrder: true); // Bouvet Island; Heard Island and McDonald Islands.

    [Fact]
    public void The_checksum_follows_the_file_and_the_seeder_version()
    {
        _files.Checksum("countries.json", 1).ShouldBe(_files.Checksum("countries.json", 1));
        _files.Checksum("countries.json", 1).ShouldMatch("^v1:[0-9a-f]{64}$");
        _files.Checksum("countries.json", 2).ShouldStartWith("v2:");
        _files.Checksum("countries.json", 1).ShouldNotBe(_files.Checksum("currencies.json", 1));
        new EditedFiles().Checksum("countries.json", 1).ShouldNotBe(_files.Checksum("countries.json", 1));
    }

    [Fact]
    public void A_file_with_a_missing_value_is_rejected() =>
        Should.Throw<System.Text.Json.JsonException>(() => new EditedFiles().Items<CountryItem>("countries.json"));

    private static void ShouldBeUnique(IEnumerable<string> values)
    {
        var list = values.ToList();
        list.Distinct(StringComparer.Ordinal).Count().ShouldBe(list.Count);
    }

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex UpperAlpha2();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex UpperAlpha3();

    [GeneratedRegex("^[a-z]{2}$")]
    private static partial Regex LowerAlpha2();

    [GeneratedRegex("^[0-9]{3}$")]
    private static partial Regex Numeric3();

    /// <summary>countries.json with Uganda's name missing.</summary>
    private sealed class EditedFiles : ReferenceDataFiles
    {
        public override byte[] Read(string file) => System.Text.Encoding.UTF8.GetBytes(
            System.Text.Encoding.UTF8.GetString(base.Read(file)).Replace(",\"name\":\"Uganda\"", "", StringComparison.Ordinal));
    }
}
