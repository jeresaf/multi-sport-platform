using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People.Consents;

namespace SportsPlatform.Modules.Registry.Features.People.Consents;

/// <summary>
/// A guardian's consent (S06 D14): who gave it, to what, and on which date; <c>version</c>, when sent, must be the
/// person's current one. The guardian's name and contact are <c>PRIVATE</c>: kept on the record, never in the audit
/// trail or logs.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecordConsentRequest(
    string? GuardianName,
    GuardianRelationship? Relationship,
    string? Contact,
    IReadOnlyList<ConsentScope>? Scopes,
    DateOnly? GivenOn,
    uint? Version = null);

/// <summary>
/// Withdrawing scopes of a consent (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on the record and
/// in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WithdrawConsentRequest(IReadOnlyList<ConsentScope>? Scopes, string? Reason, uint? Version = null);

/// <summary>A guardian's name and contact of 1–200 characters after trimming, a relationship, scopes and a date.</summary>
public sealed class RecordConsentRequestValidator : AbstractValidator<RecordConsentRequest>
{
    public RecordConsentRequestValidator()
    {
        Text(RuleFor(request => request.GuardianName), "guardianName", GuardianConsent.GuardianNameMaxLength);
        RuleFor(request => request.Relationship).NotNull().WithMessage("'relationship' must not be empty.");
        Text(RuleFor(request => request.Contact), "contact", GuardianConsent.ContactMaxLength);
        ConsentScopeRules.Apply(RuleFor(request => request.Scopes));
        RuleFor(request => request.GivenOn).NotNull().WithMessage("'givenOn' must not be empty.");
    }

    private static void Text(IRuleBuilderInitial<RecordConsentRequest, string?> rule, string name, int maxLength) =>
        rule
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage($"'{name}' must not be empty.")
            .Must(value => value!.Trim().Length <= maxLength)
            .WithMessage($"'{name}' must be at most {maxLength} characters.")
            .Must(value => !value!.Any(char.IsControl))
            .WithMessage($"'{name}' can't contain control characters.");
}

/// <summary>Scopes to withdraw and a reason of 1–500 characters after trimming.</summary>
public sealed class WithdrawConsentRequestValidator : AbstractValidator<WithdrawConsentRequest>
{
    public WithdrawConsentRequestValidator()
    {
        ConsentScopeRules.Apply(RuleFor(request => request.Scopes));
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code)
            .Must(reason => reason!.Trim().Length <= GuardianConsent.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {GuardianConsent.ReasonMaxLength} characters.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code);
    }
}

internal static class ConsentScopeRules
{
    /// <summary>One or more scopes, each once.</summary>
    public static void Apply<T>(IRuleBuilderInitial<T, IReadOnlyList<ConsentScope>?> rule) =>
        rule
            .Cascade(CascadeMode.Stop)
            .Must(scopes => scopes is { Count: > 0 })
            .WithMessage("'scopes' must name one or more scopes.")
            .Must(scopes => scopes!.Distinct().Count() == scopes!.Count)
            .WithMessage("'scopes' must name each scope once.");
}
