using System.Globalization;
using System.Text.RegularExpressions;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy.Domain.Plans;

namespace SportsPlatform.IntegrationTests.Catalogues;

/// <summary>
/// docs/architecture/authorization.md's "Entitlements and plan values" table is the source of truth for the plans (S04
/// D2): the declared entitlements and <see cref="TenancyPlans"/> must say exactly what it says.
/// </summary>
public sealed partial class AuthorizationEntitlementTableTests
{
    private const string _heading = "### Entitlements and plan values";

    [Fact]
    public void The_table_lists_every_declared_entitlement_and_no_other()
    {
        var rows = ReadTable();

        rows.Select(row => row.Code).Order(StringComparer.Ordinal)
            .ShouldBe(EntitlementCatalogue.Build(ModuleRegistry.All).Select(entry => entry.Entitlement.Code));
    }

    // S04-T1 acceptance: plan values match authorization.md's table (T1b compares the seeded rows with TenancyPlans).
    [Fact]
    public void Each_entitlement_has_the_type_and_the_STANDARD_and_PRO_values_of_the_table()
    {
        var declared = new DeclaredEntitlements(ModuleRegistry.All);
        var mismatches = new List<string>();

        foreach (var row in ReadTable())
        {
            var entitlement = declared.Find(row.Code);
            if (entitlement is null)
            {
                continue;
            }

            if (ActionCatalogue.ToConstant(entitlement.Type) != row.Type)
            {
                mismatches.Add($"{row.Code}: type {ActionCatalogue.ToConstant(entitlement.Type)} in code, {row.Type} in the table.");
                continue;
            }

            Compare(row.Code, TenancyPlans.Standard, Parse(entitlement, row.Standard), mismatches);
            Compare(row.Code, TenancyPlans.Pro, Parse(entitlement, row.Pro), mismatches);
        }

        mismatches.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("QUOTA", "2,000", "2000")]
    [InlineData("QUOTA", "unlimited", "unlimited")]
    [InlineData("BOOLEAN", "✓", "yes")]
    [InlineData("BOOLEAN", "–", "no")]
    [InlineData("LEVEL", "`FULL`: brand versions, images, footer optional", "FULL")]
    public void Table_cells_are_read_as_values(string type, string cell, string expected)
    {
        var entitlement = type switch
        {
            "QUOTA" => new EntitlementDefinition("MAX_TEAMS", "Teams", "Teams.", EntitlementType.Quota, "Registry", "S04", unit: "teams"),
            "BOOLEAN" => new EntitlementDefinition("TRANSFERS", "Transfers", "Transfers.", EntitlementType.Boolean, "Team Operations", "S04"),
            _ => new EntitlementDefinition("BRANDING", "Branding", "Branding.", EntitlementType.Level, "Branding & Sponsorship", "S04", levels: ["BASIC", "FULL"]),
        };

        Parse(entitlement, cell).ToString().ShouldBe(expected);
    }

    private static void Compare(string code, PlanDefinition plan, EntitlementValue expected, List<string> mismatches)
    {
        var actual = plan.Values.GetValueOrDefault(code);
        if (actual != expected)
        {
            mismatches.Add($"{code} on {plan.Code}: {actual?.ToString() ?? "no value"} in TenancyPlans, {expected} in the table.");
        }
    }

    private static EntitlementValue Parse(EntitlementDefinition entitlement, string cell) => entitlement.Type switch
    {
        EntitlementType.Boolean => cell switch
        {
            "✓" => EntitlementValue.Flag(true),
            "–" => EntitlementValue.Flag(false),
            _ => throw new InvalidOperationException($"{entitlement.Code}: '{cell}' is not ✓ or –."),
        },
        EntitlementType.Quota => cell == "unlimited"
            ? EntitlementValue.UnlimitedQuota
            : EntitlementValue.Quota(int.Parse(cell, NumberStyles.AllowThousands, CultureInfo.InvariantCulture)),
        EntitlementType.Level => EntitlementValue.Level(LeadingCode().Match(cell) is { Success: true } match
            ? match.Groups["code"].Value
            : throw new InvalidOperationException($"{entitlement.Code}: '{cell}' doesn't start with a `LEVEL`.")),
        _ => throw new InvalidOperationException($"Unknown entitlement type {entitlement.Type}."),
    };

    /// <summary>The rows under <see cref="_heading"/>: entitlement code, type, STANDARD and PRO cells.</summary>
    private static List<TableRow> ReadTable()
    {
        var lines = File.ReadAllLines(Path.Combine(ComposeFile.RepositoryRoot, "docs", "architecture", "authorization.md"));
        var heading = Array.FindIndex(lines, line => line.StartsWith(_heading, StringComparison.Ordinal));
        heading.ShouldBeGreaterThanOrEqualTo(0, $"authorization.md has no '{_heading}' heading.");

        var table = lines.Skip(heading + 1)
            .SkipWhile(line => !line.StartsWith('|'))
            .TakeWhile(line => line.StartsWith('|'))
            .ToList();
        table.Count.ShouldBeGreaterThan(2, "The plan values table is missing.");
        Cells(table[0]).ShouldBe(["Entitlement", "Type", "Gates (examples)", "STANDARD", "PRO"]);

        return [.. table.Skip(2).Select(line =>
        {
            var cells = Cells(line);
            cells.Count.ShouldBe(5, $"Row '{line}' doesn't have five cells.");
            return new TableRow(cells[0].Trim('`'), cells[1], cells[3], cells[4]);
        })];
    }

    // Splits on unescaped pipes; "\|" inside a cell stays.
    private static List<string> Cells(string line) =>
        [.. CellSeparator().Split(line.Trim().Trim('|')).Select(cell => cell.Trim())];

    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex CellSeparator();

    [GeneratedRegex("^`(?<code>[A-Z][A-Z0-9_]*)`")]
    private static partial Regex LeadingCode();

    private sealed record TableRow(string Code, string Type, string Standard, string Pro);
}
