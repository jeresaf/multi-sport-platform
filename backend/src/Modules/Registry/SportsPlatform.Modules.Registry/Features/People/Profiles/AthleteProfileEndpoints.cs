using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>
/// <c>POST /people/{personId}/athlete-profiles</c>, <c>PUT …/athlete-profiles/{profileId}</c> and
/// <c>POST …/athlete-profiles/{profileId}/status</c> (S06 D12). Profiles are the person's children: each takes the
/// person's version and answers the person.
/// </summary>
internal static class AthleteProfileEndpoints
{
    private const string _versionNote =
        "Send `version` or `If-Match` from the last read of the person to get 409 PERSON_CONCURRENCY_CONFLICT if the person " +
        "or any of their profiles changed since; without them, a change to the same person saved at the same moment can " +
        "still answer it. Answers the person as GET /people/{personId} does; its version moves.";

    private const string _codesNote =
        "Codes are the sport variant's (GET /sports/{variant} gives their labels): `primaryPosition` and `otherPositions` " +
        "(up to 5, distinct, not the primary one) must be positions of the variant's latest definition " +
        "(PERSON_POSITION_UNKNOWN, on the field). `publicName` is the name shown with the sport; null shows the display name.";

    public static void Map(IEndpointRouteBuilder people)
    {
        people
            .MapPost("/{personId:guid}/athlete-profiles", (
                    Guid personId,
                    CreateAthleteProfileRequest request,
                    CreateAthleteProfileHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, request, ifMatch, cancellationToken)))
            .WithName("CreateAthleteProfile")
            .WithSummary("Add an athlete profile to a person")
            .WithDescription(
                "Records that the person plays a sport variant the tenant has enabled (PERSON_SPORT_NOT_ENABLED otherwise); " +
                "the profile starts ACTIVE. A person holds one profile per variant that isn't archived " +
                "(PERSON_ATHLETE_PROFILE_EXISTS: change or reactivate that one). An archived or deceased person takes no new " +
                $"profile (PERSON_ARCHIVED, PERSON_DECEASED). {_codesNote} {_versionNote}")
            .RequireAction(RegistryActions.AthleteProfileCreate)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<CreateAthleteProfileRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.SportNotEnabled,
                RegistryErrors.PositionUnknown, RegistryErrors.AthleteProfileExists, RegistryErrors.PersonArchived,
                RegistryErrors.PersonDeceased);

        people
            .MapPut("/{personId:guid}/athlete-profiles/{profileId:guid}", (
                    Guid personId,
                    Guid profileId,
                    UpdateAthleteProfileRequest request,
                    UpdateAthleteProfileHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, profileId, request, ifMatch, cancellationToken)))
            .WithName("UpdateAthleteProfile")
            .WithSummary("Change an athlete profile")
            .WithDescription(
                "Replaces the profile's positions, dominant side and public name (a field left out is cleared); the variant " +
                "never changes. A correction: allowed whatever the person's status and whether or not the tenant still has " +
                $"the variant enabled, but not on an archived profile (PERSON_PROFILE_ARCHIVED). {_codesNote} {_versionNote}")
            .RequireAction(RegistryActions.AthleteProfileUpdate)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<UpdateAthleteProfileRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.ProfileNotFound,
                RegistryErrors.ProfileArchived, RegistryErrors.PositionUnknown);

        people
            .MapPost("/{personId:guid}/athlete-profiles/{profileId:guid}/status", (
                    Guid personId,
                    Guid profileId,
                    ChangeProfileStatusRequest request,
                    ChangeAthleteProfileStatusHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, profileId, request, ifMatch, cancellationToken)))
            .WithName("ChangeAthleteProfileStatus")
            .WithSummary("Change an athlete profile's status")
            .WithDescription(
                "Moves the profile, with a reason (body `{ \"status\": …, \"reason\": … }`, both required): ACTIVE ⇄ INACTIVE; " +
                "ACTIVE or INACTIVE → RETIRED; RETIRED → ACTIVE; any status but ARCHIVED → ARCHIVED, which is final. Any other " +
                "move is PERSON_PROFILE_STATUS_INVALID; a move to ACTIVE is refused on an archived or deceased person " +
                "(PERSON_ARCHIVED, PERSON_DECEASED). Each move is kept in the profile's history. The reason is kept " +
                $"permanently in that history and the audit trail: don't include personal data. {_versionNote}")
            .RequireAction(RegistryActions.AthleteProfileStatusChange)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<ChangeProfileStatusRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                CommonErrors.ReasonRequired, RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict,
                RegistryErrors.ProfileNotFound, RegistryErrors.ProfileStatusInvalid, RegistryErrors.PersonArchived,
                RegistryErrors.PersonDeceased);
    }
}
