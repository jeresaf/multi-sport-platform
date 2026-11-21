using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>
/// <c>POST /people/{personId}/staff-profiles</c>, <c>PUT …/staff-profiles/{profileId}</c> and
/// <c>POST …/staff-profiles/{profileId}/status</c> (S06 D12). Profiles are the person's children: each takes the
/// person's version and answers the person.
/// </summary>
internal static class StaffProfileEndpoints
{
    private const string _versionNote =
        "Send `version` or `If-Match` from the last read of the person to get 409 PERSON_CONCURRENCY_CONFLICT if the person " +
        "or any of their profiles changed since; without them, a change to the same person saved at the same moment can " +
        "still answer it. Answers the person as GET /people/{personId} does; its version moves.";

    private const string _capabilitiesNote =
        "`capabilities` holds 1–10 things the person does, each a sport variant and a staff role of that variant's latest " +
        "definition (GET /sports/{variant} gives the codes and labels; PERSON_ROLE_UNKNOWN on `capabilities[i].role`), at " +
        "most once each pair, with an optional `qualification` (free text, up to 100 characters).";

    public static void Map(IEndpointRouteBuilder people)
    {
        people
            .MapPost("/{personId:guid}/staff-profiles", (
                    Guid personId,
                    StaffProfileRequest request,
                    CreateStaffProfileHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, request, ifMatch, cancellationToken)))
            .WithName("CreateStaffProfile")
            .WithSummary("Add a staff profile to a person")
            .WithDescription(
                "Records that the person works as staff (coach, physio…) in sport variants the tenant has enabled " +
                "(PERSON_SPORT_NOT_ENABLED on `capabilities[i].variant` otherwise); the profile starts ACTIVE. A person holds one " +
                "staff profile that isn't archived (PERSON_STAFF_PROFILE_EXISTS: change or reactivate that one). An archived " +
                $"or deceased person takes no new profile (PERSON_ARCHIVED, PERSON_DECEASED). {_capabilitiesNote} {_versionNote}")
            .RequireAction(RegistryActions.StaffProfileCreate)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<StaffProfileRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.PersonConcurrencyConflict, RegistryErrors.SportNotEnabled,
                RegistryErrors.RoleUnknown, RegistryErrors.StaffProfileExists, RegistryErrors.PersonArchived,
                RegistryErrors.PersonDeceased);

        people
            .MapPut("/{personId:guid}/staff-profiles/{profileId:guid}", (
                    Guid personId,
                    Guid profileId,
                    StaffProfileRequest request,
                    UpdateStaffProfileHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, profileId, request, ifMatch, cancellationToken)))
            .WithName("UpdateStaffProfile")
            .WithSummary("Change a staff profile")
            .WithDescription(
                "Replaces the profile's capabilities as a whole: one left out is removed, one the profile already holds (same " +
                "variant and role) keeps going with the qualification sent, a new one is added. Not on an archived profile " +
                "(PERSON_PROFILE_ARCHIVED). On an archived or deceased person, held capabilities may be kept, removed or " +
                "re-qualified, but a new one is refused (PERSON_ARCHIVED, PERSON_DECEASED). A capability the profile already holds may " +
                "stay on a variant the tenant has since disabled; an added one needs its variant enabled " +
                "(PERSON_SPORT_NOT_ENABLED). Every role sent is checked against the variant's latest definition, so one a newer " +
                $"definition dropped must be replaced or left out. {_capabilitiesNote} {_versionNote}")
            .RequireAction(RegistryActions.StaffProfileUpdate)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<StaffProfileRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.PersonConcurrencyConflict, RegistryErrors.ProfileNotFound,
                RegistryErrors.ProfileArchived, RegistryErrors.SportNotEnabled, RegistryErrors.RoleUnknown,
                RegistryErrors.PersonArchived, RegistryErrors.PersonDeceased);

        people
            .MapPost("/{personId:guid}/staff-profiles/{profileId:guid}/status", (
                    Guid personId,
                    Guid profileId,
                    ChangeProfileStatusRequest request,
                    ChangeStaffProfileStatusHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, profileId, request, ifMatch, cancellationToken)))
            .WithName("ChangeStaffProfileStatus")
            .WithSummary("Change a staff profile's status")
            .WithDescription(
                "Moves the profile, with a reason (body `{ \"status\": …, \"reason\": … }`, both required): ACTIVE ⇄ INACTIVE; " +
                "ACTIVE or INACTIVE → RETIRED; RETIRED → ACTIVE; any status but ARCHIVED → ARCHIVED, which is final. Any other " +
                "move is PERSON_PROFILE_STATUS_INVALID; a move to ACTIVE is refused on an archived or deceased person " +
                "(PERSON_ARCHIVED, PERSON_DECEASED). Each move is kept in the profile's history. The reason is kept " +
                $"permanently in that history and the audit trail: don't include personal data. {_versionNote}")
            .RequireAction(RegistryActions.StaffProfileStatusChange)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<ChangeProfileStatusRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                CommonErrors.ReasonRequired, RegistryErrors.PersonNotFound, RegistryErrors.PersonConcurrencyConflict,
                RegistryErrors.ProfileNotFound, RegistryErrors.ProfileStatusInvalid, RegistryErrors.PersonArchived,
                RegistryErrors.PersonDeceased);
    }
}
