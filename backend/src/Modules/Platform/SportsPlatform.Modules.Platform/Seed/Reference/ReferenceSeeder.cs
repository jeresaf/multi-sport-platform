using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Platform.Domain.Reference;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Seed.Reference;

/// <summary>
/// Upserts one reference table from its data file by code, as the owner: new codes are inserted, changed values
/// updated, codes no longer in the file deactivated (never deleted). Unchanged rows are not written at all.
/// </summary>
public abstract class ReferenceSeeder<TItem, TEntry>(ReferenceDataFiles files) : ISeeder
    where TEntry : ReferenceEntry
{
    /// <summary>Bump when the seeder writes something different from the same file.</summary>
    private const int _version = 1;

    public abstract string Key { get; }

    public SeedCategory Category => SeedCategory.Reference;

    public int Order => 0;

    public string Checksum => files.Checksum(File, _version);

    /// <summary>The data file, e.g. <c>countries.json</c>.</summary>
    protected abstract string File { get; }

    protected abstract string CodeOf(TItem item);

    protected abstract TEntry Create(TItem item);

    /// <summary>Takes the item's values; returns whether anything changed (a reactivation counts).</summary>
    protected abstract bool Apply(TEntry entry, TItem item);

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = files.Items<TItem>(File);
        var db = context.CreateOwnerContext<PlatformDbContext>();
        var entries = await db.Set<TEntry>().ToDictionaryAsync(e => e.Code, StringComparer.Ordinal, cancellationToken);
        var listed = items.Select(CodeOf).ToHashSet(StringComparer.Ordinal);

        // Two saves in one transaction: deactivations are written before any insert or update, so a new entry may take
        // the alpha-3 or numeric code of one dropped in the same update (those are unique among active rows only).
        // EF Core doesn't promise that order within a single save.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var deactivated = 0;
        foreach (var entry in entries.Values.Where(entry => !listed.Contains(entry.Code)))
        {
            deactivated += entry.Deactivate() ? 1 : 0;
        }

        await db.SaveChangesAsync(cancellationToken);

        int inserted = 0, updated = 0;
        foreach (var item in items)
        {
            if (entries.TryGetValue(CodeOf(item), out var entry))
            {
                updated += Apply(entry, item) ? 1 : 0;
            }
            else
            {
                db.Add(Create(item));
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SeedOutcome(inserted, updated, deactivated);
    }
}

public sealed class CountrySeeder(ReferenceDataFiles files) : ReferenceSeeder<CountryItem, Country>(files)
{
    public const string SeederKey = "platform.reference.countries";

    public override string Key => SeederKey;

    protected override string File => "countries.json";

    protected override string CodeOf(CountryItem item) => item.Code;

    protected override Country Create(CountryItem item) => new(item.Code, item.Alpha3Code, item.NumericCode, item.Name);

    protected override bool Apply(Country entry, CountryItem item) => entry.Apply(item.Alpha3Code, item.NumericCode, item.Name);
}

public sealed class CurrencySeeder(ReferenceDataFiles files) : ReferenceSeeder<CurrencyItem, Currency>(files)
{
    public const string SeederKey = "platform.reference.currencies";

    public override string Key => SeederKey;

    protected override string File => "currencies.json";

    protected override string CodeOf(CurrencyItem item) => item.Code;

    protected override Currency Create(CurrencyItem item) => new(item.Code, item.NumericCode, item.Name, item.MinorUnits);

    protected override bool Apply(Currency entry, CurrencyItem item) => entry.Apply(item.NumericCode, item.Name, item.MinorUnits);
}

public sealed class LanguageSeeder(ReferenceDataFiles files) : ReferenceSeeder<LanguageItem, Language>(files)
{
    public const string SeederKey = "platform.reference.languages";

    public override string Key => SeederKey;

    protected override string File => "languages.json";

    protected override string CodeOf(LanguageItem item) => item.Code;

    protected override Language Create(LanguageItem item) => new(item.Code, item.Name);

    protected override bool Apply(Language entry, LanguageItem item) => entry.Apply(item.Name);
}

public sealed class TimeZoneSeeder(ReferenceDataFiles files) : ReferenceSeeder<TimeZoneItem, IanaTimeZone>(files)
{
    public const string SeederKey = "platform.reference.time-zones";

    public override string Key => SeederKey;

    protected override string File => "time-zones.json";

    protected override string CodeOf(TimeZoneItem item) => item.Code;

    protected override IanaTimeZone Create(TimeZoneItem item) => new(item.Code, item.CountryCode);

    protected override bool Apply(IanaTimeZone entry, TimeZoneItem item) => entry.Apply(item.CountryCode);
}
