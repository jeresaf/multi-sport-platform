using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.Modules.Registry.Features.Teams.RenameTeam;

/// <summary>
/// A team's new name from a date (S07 D8). <c>shortName</c> and <c>code</c> are the values from then on: left out, the
/// team keeps its current one; sent as <c>null</c>, it is cleared (owner, S07-T3c plan). After the duplicate warning,
/// <c>duplicatesReviewed</c> with an optional reason. <c>version</c>, when sent, must be the team's current one (or
/// <c>If-Match</c>).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RenameTeamRequest
{
    private readonly string? _shortName;
    private readonly string? _code;

    /// <summary>
    /// <paramref name="name"/> and <paramref name="effectiveOn"/> are constructor parameters so OpenAPI lists them as
    /// required, as a positional record's; one left out still binds as null and the validator names it.
    /// </summary>
    [JsonConstructor]
    public RenameTeamRequest(string? name, DateOnly? effectiveOn)
    {
        Name = name;
        EffectiveOn = effectiveOn;
    }

    public string? Name { get; }

    /// <summary>The short name from <c>effectiveOn</c>; left out keeps the current one, <c>null</c> clears it.</summary>
    public string? ShortName
    {
        get => _shortName;
        init
        {
            _shortName = value;
            ShortNameSent = true;
        }
    }

    /// <summary>The code from <c>effectiveOn</c>; left out keeps the current one, <c>null</c> clears it.</summary>
    public string? Code
    {
        get => _code;
        init
        {
            _code = value;
            CodeSent = true;
        }
    }

    /// <summary>The new name's first day: after the current name's first day, not after today (the tenant's date).</summary>
    public DateOnly? EffectiveOn { get; }

    public bool DuplicatesReviewed { get; init; }

    public string? DuplicateReason { get; init; }

    public uint? Version { get; init; }

    /// <summary>Whether the body named <c>shortName</c> (null included); the JSON reader sets a member only when it is there.</summary>
    [JsonIgnore]
    public bool ShortNameSent { get; private init; }

    /// <summary>Whether the body named <c>code</c> (null included).</summary>
    [JsonIgnore]
    public bool CodeSent { get; private init; }
}

/// <summary>
/// The request's shape: a name of 1–120 characters after trimming, a short name of at most 30, a code of three
/// upper-case letters, an <c>effectiveOn</c>, and a duplicate reason that isn't blank and comes only with
/// <c>duplicatesReviewed</c>. The date against the current name and today is the handler's.
/// </summary>
public sealed partial class RenameTeamRequestValidator : AbstractValidator<RenameTeamRequest>
{
    public RenameTeamRequestValidator()
    {
        TeamText.Rule(RuleFor(request => request.Name), "name", TeamDetails.NameMaxLength, required: true);
        TeamText.Rule(RuleFor(request => request.ShortName), "shortName", TeamDetails.ShortNameMaxLength, required: false);
        RuleFor(request => request.Code)
            .Must(code => code is null || CodePattern().IsMatch(code))
            .WithMessage("'code' must be three upper-case letters (A–Z).");
        RuleFor(request => request.EffectiveOn).NotNull().WithMessage("'effectiveOn' is required.");
        RuleFor(request => request.DuplicateReason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => reason is null || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'duplicateReason' must not be empty when sent.")
            .Must(reason => reason is null || reason.Trim().Length <= ITeamChangeRequest.DuplicateReasonMaxLength)
            .WithMessage($"'duplicateReason' must be at most {ITeamChangeRequest.DuplicateReasonMaxLength} characters.");
        RuleFor(request => request.DuplicateReason)
            .Null().When(request => !request.DuplicatesReviewed)
            .WithMessage("'duplicateReason' comes only with 'duplicatesReviewed'.");
    }

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
