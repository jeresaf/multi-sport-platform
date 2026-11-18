using Microsoft.AspNetCore.Http.HttpResults;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>
/// What the duplicate check compares, as the check, a create and an update (S06-T3) send it (S06 D5, D6, D8). The
/// requests implement it so they share one validator (<see cref="PersonDetailsValidator{T}"/>) and one set of handler
/// rules (<see cref="PersonDetailsRules"/>).
/// </summary>
public interface IPersonDetails
{
    string? FirstName { get; }

    string? LastName { get; }

    string? MiddleName { get; }

    string? OtherNames { get; }

    DateOnly? DateOfBirth { get; }

    string? Nationality { get; }

    string? SecondaryNationality { get; }

    IReadOnlyList<IIdentifierDetails?>? ExternalIdentifiers { get; }
}

/// <summary>The whole biodata a create or an update saves (S06 D2): the details the check compares and the rest.</summary>
public interface IPersonBiodataDetails : IPersonDetails
{
    string? DisplayName { get; }

    int? HeightCm { get; }

    decimal? WeightKg { get; }
}

/// <summary>An external identifier as sent: the authority's code and the value it gave.</summary>
public interface IIdentifierDetails
{
    string? Authority { get; }

    string? Value { get; }
}

/// <summary>
/// A refusal that belongs to one field of the request (<paramref name="Field"/>, camelCase), or to none, with a
/// <paramref name="Detail"/> when the refusal has more to say than the error's message (a quota's limit).
/// </summary>
public sealed record FieldRefusal(Error Error, string? Field = null, string? Detail = null)
{
    /// <summary>Problem Details with the error's code, and the field in <c>errors</c> when there is one.</summary>
    public ProblemHttpResult ToProblem()
    {
        var problem = Error.ToProblem(Detail);
        if (Field is { } field)
        {
            problem.ProblemDetails.Extensions[ProblemFields.Errors] = new List<FieldError> { new(field, Error.Code) };
        }

        return problem;
    }
}

/// <summary>
/// The rules on person details that need more than the request's shape, with their own codes (422): a first or last
/// name without a letter or a digit has no name key (<c>PERSON_NAME_INVALID</c>); an authority naming a national
/// identity document is not collected (<c>PERSON_EXTERNAL_ID_AUTHORITY_RESERVED</c>, S06 D10); a date of birth after
/// the tenant's date is refused (<c>PERSON_DATE_OF_BIRTH_IN_FUTURE</c>, owner, Q43).
/// </summary>
public static class PersonDetailsRules
{
    /// <summary>The first rule <paramref name="details"/> breaks on <paramref name="today"/>, or null.</summary>
    public static FieldRefusal? Refuse(IPersonDetails details, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (NameNormaliser.Normalise(details.FirstName).Length == 0)
        {
            return new(RegistryErrors.NameInvalid, "firstName");
        }

        if (NameNormaliser.Normalise(details.LastName).Length == 0)
        {
            return new(RegistryErrors.NameInvalid, "lastName");
        }

        if (details.DateOfBirth > today)
        {
            return new(RegistryErrors.DateOfBirthInFuture, "dateOfBirth");
        }

        var identifiers = details.ExternalIdentifiers ?? [];
        for (var i = 0; i < identifiers.Count; i++)
        {
            if (ExternalIdentifier.ReservedAuthorities.Contains(identifiers[i]!.Authority!))
            {
                return new(RegistryErrors.ExternalIdAuthorityReserved, $"externalIdentifiers[{i}].authority");
            }
        }

        return null;
    }

    /// <summary>The normalised input of the duplicate check; <paramref name="personId"/> is the person being changed.</summary>
    public static DuplicateCheckInput ToCheckInput(IPersonDetails details, Guid? personId)
    {
        ArgumentNullException.ThrowIfNull(details);

        return new DuplicateCheckInput(
            NameNormaliser.NameKey(details.FirstName, details.MiddleName, details.LastName, details.OtherNames),
            NameNormaliser.Normalise(details.LastName),
            details.DateOfBirth,
            details.Nationality,
            details.SecondaryNationality,
            [.. (details.ExternalIdentifiers ?? []).Select(identifier => (identifier!.Authority!, identifier.Value!.Trim()))],
            personId);
    }
}
