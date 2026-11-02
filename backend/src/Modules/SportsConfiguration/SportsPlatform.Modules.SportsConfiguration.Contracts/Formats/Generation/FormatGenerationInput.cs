using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;

/// <summary>
/// What a generator reads. <see cref="Entrants"/> are the entrants' keys in order (the seeds' order for <c>SEEDED</c>);
/// the keys mean nothing to the generator. A null entry is a bye, accepted only in a knockout's <c>MANUAL</c> bracket
/// lines. <see cref="Parameters"/> left out take the format's defaults. <see cref="Seed"/> drives <c>RANDOM</c>.
/// </summary>
public sealed record FormatGenerationInput(
    IReadOnlyList<string?> Entrants,
    IReadOnlyDictionary<string, ParameterValue> Parameters,
    ulong Seed);
