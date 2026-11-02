using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Generators;

/// <summary>
/// The fixture generators (S05 D10): checks the input against the format, fills the parameters' defaults, runs the
/// stage's generator and hashes the result with its inputs (<see cref="GeneratedFormatHash"/>). Pure: no clock, files,
/// database or <c>System.Random</c> (architecture test).
/// </summary>
public sealed class FormatGenerators : IFormatGenerators
{
    // The third-place contest needs two semi-finals without a bye.
    private const int _thirdPlaceMinimum = 4;

    public Result<GeneratedFormat> Generate(FormatDefinition format, FormatGenerationInput input)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(input);

        // Every format of S05 has one stage; GROUPS_THEN_KNOCKOUT (S15) brings the first with more.
        if (format.Stages.Count != 1 || !FormatGeneratorKinds.Known.TryGetValue(format.Stages[0].Generator, out var kind))
        {
            throw new NotSupportedException($"Format {format.Code}@{format.Version}: only one stage with a known generator can be generated.");
        }

        var stage = format.Stages[0];
        var parameters = ResolveParameters(format, input.Parameters);
        if (parameters.IsFailure)
        {
            return parameters.Error;
        }

        var manual = stage.Generator == FormatGeneratorKinds.Knockout
            && parameters.Value[FormatParameters.Seeding].Choice == Seedings.Manual;
        var entrants = CheckEntrants(input.Entrants, manual);
        if (entrants.IsFailure)
        {
            return entrants.Error;
        }

        var count = entrants.Value.Count;
        if (count < Math.Max(format.Participants.Min, kind.MinParticipants)
            || count > Math.Min(format.Participants.Max, kind.MaxParticipants ?? int.MaxValue))
        {
            return Detailed(
                SportsConfigurationErrors.FormatParticipantCountInvalid,
                $"{format.Code} takes {format.Participants.Min} to {format.Participants.Max} participants, not {count}");
        }

        var random = new SeededRandom(input.Seed);
        IReadOnlyList<GeneratedRound> rounds;
        switch (stage.Generator)
        {
            case FormatGeneratorKinds.SingleContest:
                rounds = [new GeneratedRound(1, RoundCodes.Round(1), BracketPlaces: null, [
                    new GeneratedPairing($"{RoundCodes.Round(1)}_1", PairingSide.Entrant(entrants.Value[0]), PairingSide.Entrant(entrants.Value[1])),
                ])];
                break;
            case FormatGeneratorKinds.RoundRobin:
                rounds = RoundRobinGenerator.Generate(entrants.Value, parameters.Value, random);
                break;
            case FormatGeneratorKinds.Knockout:
                var thirdPlace = parameters.Value[FormatParameters.ThirdPlaceContest].Flag;
                if (thirdPlace && count < _thirdPlaceMinimum)
                {
                    return Detailed(
                        SportsConfigurationErrors.FormatParameterInvalid,
                        $"{FormatParameters.ThirdPlaceContest} needs {_thirdPlaceMinimum} participants or more, not {count}");
                }

                var lines = KnockoutGenerator.Lines(input.Entrants, parameters.Value, random);
                if (manual)
                {
                    var bracketError = CheckBracketLines(lines, count);
                    if (bracketError is not null)
                    {
                        return bracketError;
                    }
                }

                rounds = KnockoutGenerator.Generate(lines, thirdPlace);
                break;
            default:
                // A generator added to FormatGeneratorKinds (SERIES, S16) needs its own case here.
                throw new NotSupportedException($"Format {format.Code}@{format.Version}: the generator {stage.Generator} isn't built yet.");
        }

        var stages = new[] { new GeneratedStage(stage.Code, stage.Type, rounds) };
        var givenEntrants = input.Entrants.ToArray();
        return Result.Success(new GeneratedFormat(
            format.Code,
            format.Version,
            parameters.Value,
            input.Seed,
            givenEntrants,
            stages,
            GeneratedFormatHash.Compute(format.Code, format.Version, parameters.Value, input.Seed, givenEntrants, stages)));
    }

    private static Result<IReadOnlyDictionary<string, ParameterValue>> ResolveParameters(
        FormatDefinition format, IReadOnlyDictionary<string, ParameterValue> given)
    {
        ArgumentNullException.ThrowIfNull(given);
        foreach (var (code, value) in given.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!format.Parameters.TryGetValue(code, out var definition))
            {
                return Detailed(SportsConfigurationErrors.FormatParameterInvalid, $"{format.Code} has no parameter {code}");
            }

            var problem = value is null ? "has no value" : Problem(definition, value);
            if (problem is not null)
            {
                return Detailed(SportsConfigurationErrors.FormatParameterInvalid, $"{code} {problem}");
            }
        }

        var resolved = new SortedDictionary<string, ParameterValue>(StringComparer.Ordinal);
        foreach (var (code, definition) in format.Parameters)
        {
            resolved[code] = given.TryGetValue(code, out var value) ? value : definition.Default;
        }

        return Result.Success<IReadOnlyDictionary<string, ParameterValue>>(resolved);
    }

    private static string? Problem(ParameterDefinition definition, ParameterValue value)
    {
        if (value.Type != definition.Type)
        {
            return $"is a {Describe(definition.Type)}, not {value}";
        }

        return definition.Type switch
        {
            ParameterType.Number when value.Number < definition.Min || value.Number > definition.Max =>
                $"is from {definition.Min} to {definition.Max}, not {value}",
            ParameterType.Choice when !definition.Values.Contains(value.Choice, StringComparer.Ordinal) =>
                $"is one of {string.Join(", ", definition.Values)}, not {value}",
            _ => null,
        };
    }

    private static string Describe(ParameterType type) => type switch
    {
        ParameterType.Number => "whole number",
        ParameterType.Flag => "flag",
        _ => "choice",
    };

    // Keys are trimmed text, each once; a bye (null) only in MANUAL bracket lines. The entrants left are the non-byes.
    private static Result<IReadOnlyList<string>> CheckEntrants(IReadOnlyList<string?> given, bool byesAllowed)
    {
        ArgumentNullException.ThrowIfNull(given);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < given.Count; i++)
        {
            var key = given[i];
            if (key is null)
            {
                if (!byesAllowed)
                {
                    return Detailed(SportsConfigurationErrors.FormatEntrantsInvalid, $"entrant {i + 1} is a bye, allowed only in MANUAL bracket lines");
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(key) || key.Trim().Length != key.Length)
            {
                return Detailed(SportsConfigurationErrors.FormatEntrantsInvalid, $"entrant {i + 1} has a blank or untrimmed key");
            }

            if (!seen.Add(key))
            {
                return Detailed(SportsConfigurationErrors.FormatEntrantsInvalid, $"entrant {key} is given twice");
            }
        }

        return Result.Success<IReadOnlyList<string>>([.. given.Where(key => key is not null).Select(key => key!)]);
    }

    // MANUAL lines: as many as the bracket for their entrants, and no pairing of two byes.
    private static Error? CheckBracketLines(IReadOnlyList<string?> lines, int entrants)
    {
        var size = KnockoutGenerator.BracketSize(entrants);
        if (lines.Count != size)
        {
            return Detailed(
                SportsConfigurationErrors.FormatEntrantsInvalid,
                $"{entrants} participants need {size} bracket lines, not {lines.Count}");
        }

        for (var i = 0; i < size; i += 2)
        {
            if (lines[i] is null && lines[i + 1] is null)
            {
                return Detailed(SportsConfigurationErrors.FormatEntrantsInvalid, $"bracket lines {i + 1} and {i + 2} are both byes");
            }
        }

        return null;
    }

    // The catalogue's error with what was wrong; the code and kind stay the catalogue's.
    private static Error Detailed(Error error, string detail) => new(error.Code, $"{error.Message} ({detail}.)", error.Kind);
}
