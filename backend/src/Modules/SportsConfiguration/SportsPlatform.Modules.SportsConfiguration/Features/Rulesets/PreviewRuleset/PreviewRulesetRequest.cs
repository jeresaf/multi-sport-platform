using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.PreviewRuleset;

/// <summary>
/// The preview's body as the API documents it: one of the template's formats with its parameters, and the changes to
/// the template's editable rules keyed by their dotted paths (<c>standings.points.win</c>). The endpoint reads it as
/// <see cref="PreviewRulesetBody"/>, so a value of the wrong shape is answered on its own field.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewRulesetRequest(
    PreviewRulesetFormatRequest Format,
    IReadOnlyDictionary<string, ParameterValue>? Overrides = null);

/// <summary>A format of the template: its code, a version (the latest when left out) and its parameters by code.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewRulesetFormatRequest(
    string Code,
    int? Version = null,
    IReadOnlyDictionary<string, ParameterValue>? Parameters = null);

/// <summary>
/// <see cref="PreviewRulesetRequest"/> as read: the values stay JSON until the handler reads each one, so <c>null</c>,
/// <c>1.5</c> or an object is a problem on <c>overrides.&lt;path&gt;</c> or <c>format.parameters.&lt;code&gt;</c>, not
/// an unreadable body.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewRulesetBody(
    PreviewRulesetFormatBody? Format = null,
    IReadOnlyDictionary<string, JsonElement>? Overrides = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewRulesetFormatBody(
    string? Code = null,
    int? Version = null,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null);

/// <summary>The body's shape: a format with a code, and a version from 1 when one is sent.</summary>
public sealed class PreviewRulesetBodyValidator : AbstractValidator<PreviewRulesetBody>
{
    public PreviewRulesetBodyValidator()
    {
        RuleFor(body => body.Format)
            .NotNull()
            .OverridePropertyName("format")
            .WithMessage("'format' is required: one of the template's formats, with its code.");

        When(body => body.Format is not null, () =>
        {
            RuleFor(body => body.Format!.Code)
                .NotEmpty()
                .OverridePropertyName("format.code")
                .WithMessage("'format.code' is required: the code of one of the template's formats.");

            RuleFor(body => body.Format!.Version)
                .GreaterThanOrEqualTo(1)
                .When(body => body.Format!.Version is not null)
                .OverridePropertyName("format.version")
                .WithMessage("'format.version' must be a whole number from 1, or left out for the latest.");
        });
    }
}
