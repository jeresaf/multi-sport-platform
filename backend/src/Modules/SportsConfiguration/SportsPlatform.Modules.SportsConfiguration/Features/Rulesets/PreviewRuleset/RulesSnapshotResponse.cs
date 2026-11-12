using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.PreviewRuleset;

/// <summary>
/// An effective-rules snapshot in the API's names (docs/domain/rulesets.md): the exact sport definition, format and
/// template versions, the format's parameters with the defaults filled in, the rules and the snapshot's <c>hash</c>.
/// The hash is of the snapshot's canonical JSON (snake_case, RFC 8785), which an edition stores (S09); this answer
/// carries the same content in camelCase.
/// </summary>
public sealed record RulesSnapshotResponse(
    RulesSnapshotSportResponse Sport,
    RulesSnapshotFormatResponse Format,
    RulesSnapshotTemplateResponse Template,
    RulesetValuesResponse Values,
    string Hash)
{
    public static RulesSnapshotResponse From(RulesSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(
            new(snapshot.Sport.Variant, snapshot.Sport.Version, snapshot.Sport.Hash),
            new(snapshot.Format.Code, snapshot.Format.Version, snapshot.Format.Parameters
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .ToDictionary(parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal)),
            new(snapshot.Template.Code, snapshot.Template.Version),
            RulesetValuesResponse.From(snapshot.Values),
            snapshot.Hash);
    }
}

/// <summary>The sport definition the template pins, with its content hash.</summary>
public sealed record RulesSnapshotSportResponse(string Variant, int Version, string Hash);

/// <summary>The format at its exact version, with every parameter (the ones sent and the defaults) by code.</summary>
public sealed record RulesSnapshotFormatResponse(string Code, int Version, IReadOnlyDictionary<string, ParameterValue> Parameters);

public sealed record RulesSnapshotTemplateResponse(string Code, int Version);
