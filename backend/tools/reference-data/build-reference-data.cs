// Regenerates Platform's reference data files from their pinned sources (Data/README.md lists them and their
// licences). Run from the repository root:
//   dotnet run backend/tools/reference-data/build-reference-data.cs
// Review the diff of the four files, then commit them; the REFERENCE seeders pick up the change by checksum.
#:property PublishAot=false
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

const string Cldr = "48.0.0";
const string Tzdata = "2026c";
const string CldrBase = $"https://raw.githubusercontent.com/unicode-org/cldr-json/{Cldr}/cldr-json";
const string TzBase = $"https://raw.githubusercontent.com/eggert/tz/{Tzdata}";
const string IsoCurrencies = "https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml";
const string LocLanguages = "https://www.loc.gov/standards/iso639-2/ISO-639-2_utf-8.txt";
const string OutputDirectory = "backend/src/Modules/Platform/SportsPlatform.Modules.Platform/Seed/Reference/Data";

if (!Directory.Exists(OutputDirectory))
{
    Console.Error.WriteLine($"Run from the repository root: {OutputDirectory} not found.");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("msp-reference-data-build/1");

var territories = (await GetJsonAsync($"{CldrBase}/cldr-localenames-full/main/en/territories.json"))
    ["main"]!["en"]!["localeDisplayNames"]!["territories"]!.AsObject();
var languageNames = (await GetJsonAsync($"{CldrBase}/cldr-localenames-full/main/en/languages.json"))
    ["main"]!["en"]!["localeDisplayNames"]!["languages"]!.AsObject();
var currencyNames = (await GetJsonAsync($"{CldrBase}/cldr-numbers-full/main/en/currencies.json"))
    ["main"]!["en"]!["numbers"]!["currencies"]!.AsObject();
var codeMappings = (await GetJsonAsync($"{CldrBase}/cldr-core/supplemental/codeMappings.json"))
    ["supplemental"]!["codeMappings"]!.AsObject();

// Countries: the officially assigned ISO 3166-1 codes as tzdata keeps them (CLDR also maps user-assigned codes
// such as XK), with CLDR's alpha-3 and numeric codes and English names.
var countries = TabRows(await GetTextAsync($"{TzBase}/iso3166.tab"))
    .Select(row => row[0])
    .Order(StringComparer.Ordinal)
    .Select(code =>
    {
        var mapping = codeMappings[code] ?? throw new InvalidDataException($"CLDR has no code mapping for {code}.");
        return new JsonObject
        {
            ["code"] = code,
            ["alpha3Code"] = Required(mapping["_alpha3"], $"alpha-3 code of {code}"),
            ["numericCode"] = Required(mapping["_numeric"], $"numeric code of {code}"),
            ["name"] = Required(territories[code], $"English name of {code}"),
        };
    })
    .ToList();

// Currencies: ISO 4217 list one (current codes), CLDR's English names where it has one.
var isoList = XDocument.Parse(await GetTextAsync(IsoCurrencies));
var published = isoList.Root!.Attribute("Pblshd")!.Value;
var currencies = isoList.Descendants("CcyNtry")
    .Where(entry => entry.Element("Ccy") is not null)
    .GroupBy(entry => entry.Element("Ccy")!.Value, StringComparer.Ordinal)
    .OrderBy(group => group.Key, StringComparer.Ordinal)
    .Select(group =>
    {
        var entry = group.First();
        var minorUnits = entry.Element("CcyMnrUnts")!.Value;
        return new JsonObject
        {
            ["code"] = group.Key,
            ["numericCode"] = entry.Element("CcyNbr")!.Value,
            ["name"] = currencyNames[group.Key]?["displayName"]?.GetValue<string>() ?? entry.Element("CcyNm")!.Value.Trim(),
            // "N.A." for codes with no minor unit, such as gold (XAU).
            ["minorUnits"] = int.TryParse(minorUnits, CultureInfo.InvariantCulture, out var units) ? units : null,
        };
    })
    .ToList();

// Languages: every ISO 639-1 code from the Library of Congress table (alpha-3 B|T|alpha-2|English|French), CLDR's
// English names where it has one.
var languages = (await GetTextAsync(LocLanguages)).TrimStart('﻿')
    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(line => line.Split('|'))
    .Where(fields => fields[2].Length == 2)
    .OrderBy(fields => fields[2], StringComparer.Ordinal)
    .Select(fields => new JsonObject
    {
        ["code"] = fields[2],
        ["name"] = languageNames[fields[2]]?.GetValue<string>() ?? fields[3].Split(';')[0].Trim(),
    })
    .ToList();

// Time zones: zone.tab, so each country keeps its own zones (zone1970.tab merges Africa/Kampala into Africa/Nairobi),
// plus UTC.
var timeZones = TabRows(await GetTextAsync($"{TzBase}/zone.tab"))
    .Select(row => (Code: row[2], CountryCode: (string?)row[0]))
    .Append(("Etc/UTC", null))
    .OrderBy(zone => zone.Code, StringComparer.Ordinal)
    .Select(zone => new JsonObject { ["code"] = zone.Code, ["countryCode"] = zone.CountryCode })
    .ToList();

await WriteAsync("countries.json", $"ISO 3166-1 codes: IANA tzdata {Tzdata} iso3166.tab; alpha-3, numeric codes and names: Unicode CLDR {Cldr}", $"tzdata {Tzdata}, CLDR {Cldr}", countries);
await WriteAsync("currencies.json", $"ISO 4217 list one (SIX), names: Unicode CLDR {Cldr}", $"ISO 4217 {published}, CLDR {Cldr}", currencies);
await WriteAsync("languages.json", $"ISO 639-1 codes: Library of Congress ISO 639-2 table; names: Unicode CLDR {Cldr}", $"CLDR {Cldr}", languages);
await WriteAsync("time-zones.json", $"IANA tzdata {Tzdata} zone.tab, plus Etc/UTC", $"tzdata {Tzdata}", timeZones);
return 0;

async Task<JsonNode> GetJsonAsync(string url) => JsonNode.Parse(await GetTextAsync(url))!;

async Task<string> GetTextAsync(string url)
{
    Console.WriteLine($"GET {url}");
    return await http.GetStringAsync(new Uri(url));
}

static IEnumerable<string[]> TabRows(string text) => text
    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
    .Where(line => !line.StartsWith('#'))
    .Select(line => line.Split('\t'));

static string Required(JsonNode? node, string what) =>
    node?.GetValue<string>() ?? throw new InvalidDataException($"CLDR has no {what}.");

// One item per line keeps the diffs of a data update readable.
static async Task WriteAsync(string file, string source, string version, List<JsonObject> items)
{
    var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    var text = new StringBuilder()
        .Append("{\n  \"source\": ").Append(JsonSerializer.Serialize(source, options))
        .Append(",\n  \"version\": ").Append(JsonSerializer.Serialize(version, options))
        .Append(",\n  \"items\": [\n")
        .AppendJoin(",\n", items.Select(item => "    " + item.ToJsonString(options)))
        .Append("\n  ]\n}\n")
        .ToString();
    await File.WriteAllTextAsync(Path.Combine(OutputDirectory, file), text, new UTF8Encoding(false));
    Console.WriteLine($"{file}: {items.Count} items");
}
