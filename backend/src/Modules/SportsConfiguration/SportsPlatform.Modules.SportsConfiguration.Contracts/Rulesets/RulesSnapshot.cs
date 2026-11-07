using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

/// <summary>
/// An effective-rules snapshot (docs/domain/rulesets.md): the exact sport definition, format and template versions, the
/// format's parameters (defaults filled in) and the rules a competition plays by. <see cref="CanonicalJson"/> is its
/// RFC 8785 form with the keys of rulesets.md's example (snake_case; the rules' sections at the top level beside
/// <c>sport</c>, <c>format</c> and <c>template</c>); <see cref="Hash"/> is <c>sha256:</c> and the hex SHA-256 of it. An
/// edition stores the canonical JSON and its hash (S09); operational code reads the snapshot, never the template (AR-14).
/// </summary>
public sealed record RulesSnapshot(
    RulesSnapshotSport Sport,
    RulesSnapshotFormat Format,
    RulesSnapshotTemplate Template,
    RulesetValues Values,
    string CanonicalJson,
    string Hash);

/// <summary>The sport definition the template pins, with its content hash.</summary>
public sealed record RulesSnapshotSport(string Variant, int Version, string Hash);

/// <summary>The format at its exact version, with every parameter (the given ones and the defaults), by code.</summary>
public sealed record RulesSnapshotFormat(string Code, int Version, IReadOnlyDictionary<string, ParameterValue> Parameters);

public sealed record RulesSnapshotTemplate(string Code, int Version);
