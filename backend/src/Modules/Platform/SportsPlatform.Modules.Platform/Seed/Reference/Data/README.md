# Reference data files

Embedded in the Platform module and written to `platform.countries`, `currencies`, `languages` and `time_zones` by
the REFERENCE seeders (`Seed/Reference/ReferenceSeeder.cs`). Don't edit them by hand: regenerate them from the
repository root with

```bash
dotnet run backend/tools/reference-data/build-reference-data.cs
```

then review the diff and commit. The script pins the CLDR and tzdata releases (constants at its top); the ISO 4217
and ISO 639 lists are fetched as currently published, and the currency file records the ISO list's publication
date. A changed file changes its seeder's checksum, so the next `seed --category REFERENCE` applies it: new codes
are inserted, changed values updated, and codes no longer listed deactivated (never deleted).

| File | Entries | Codes from | Names from |
|---|---|---|---|
| `countries.json` | ISO 3166-1, officially assigned codes only (no `XK`) | IANA tzdata 2026c `iso3166.tab`; alpha-3 and numeric codes from CLDR 48 `codeMappings` | CLDR 48, English |
| `currencies.json` | ISO 4217 list one (current currencies and funds); minor units, null where ISO says "N.A." | ISO 4217 list one, published by SIX | CLDR 48, English; ISO name where CLDR has none |
| `languages.json` | every ISO 639-1 code | Library of Congress ISO 639-2 table (its ISO 639-1 column) | CLDR 48, English; LoC name where CLDR has none |
| `time-zones.json` | IANA zones from `zone.tab` with their country, plus `Etc/UTC` | IANA tzdata 2026c | — |

`zone.tab` (not `zone1970.tab`) keeps one zone per country, so `Africa/Kampala` stays a zone of its own instead of
an alias of `Africa/Nairobi`. Bouvet Island (`BV`) and Heard Island and McDonald Islands (`HM`) have no zone.

## Licences

- Unicode CLDR: Unicode License v3 (`Unicode-3.0`), notice in [LICENSE-Unicode.txt](LICENSE-Unicode.txt), which
  must stay with these files.
- IANA tzdata: public domain.
- ISO 3166-1, ISO 4217 and ISO 639-1 codes are published by their maintenance agencies (ISO 3166/MA, SIX for
  ISO 4217, the Library of Congress for ISO 639-2); the files hold the codes, not the ISO standards' text.
