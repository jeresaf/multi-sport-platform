using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>Why a draft may not be recorded: the errors in the order the checks ran; none when it may.</summary>
public sealed record ValidationResult(IReadOnlyList<Error> Errors)
{
    public static ValidationResult Valid { get; } = new([]);

    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Invalid(params Error[] errors) => new(errors);
}
