using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.Engines;

// sport-engine.md §11 and S05 T2's acceptance: every rule has a passing and a rejecting scenario.
public sealed class ScenarioRuleCoverageTests
{
    private static readonly IReadOnlyList<(string File, Scenario Scenario)> _scenarios =
        [.. ScenarioTests.Files().Select(row => (row.Data, Scenario.Read(Path.Combine(Scenario.Folder, row.Data))))];

    private static readonly HashSet<string> _generic = [.. GenericEventTypes.All.Select(type => type.Code)];

    [Fact]
    public void Every_scenario_covers_known_rules()
    {
        var known = ScenarioRules.All.Select(rule => rule.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var (file, scenario) in _scenarios)
        {
            scenario.Covers.ShouldNotBeEmpty($"{file} covers no rule");
            scenario.Covers.Where(code => !known.Contains(code)).ShouldBeEmpty($"{file} covers unknown rules");
        }
    }

    [Fact]
    public void Every_rule_has_a_passing_and_a_rejecting_scenario()
    {
        foreach (var rule in ScenarioRules.All)
        {
            var covering = _scenarios.Where(item => item.Scenario.Covers.Contains(rule.Code)).ToList();
            // A period start is accepted in every scenario, so a passing scenario must record an event of the sport (any
            // of the definition's types: a rule doesn't name the types it governs).
            covering.ShouldContain(
                item => item.Scenario.Events.Any(step => step.Rejected.Count == 0 && !_generic.Contains(step.Type)),
                $"{rule.Code} has no passing scenario");
            if (rule.AcceptOnly is null)
            {
                rule.Codes.ShouldNotBeEmpty($"{rule.Code} names no error codes");
                covering.ShouldContain(
                    item => item.Scenario.Events.Any(step => step.Rejected.Any(rule.Codes.Contains)),
                    $"{rule.Code} has no scenario refusing an event with its codes");
            }
        }
    }
}
