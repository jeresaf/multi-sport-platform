using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.PublicProfile;

/// <summary>
/// <c>POST /people/{personId}/public-profile/enable</c> and <c>…/disable</c> (S06 D15). No body: send <c>If-Match</c>
/// from the last read.
/// </summary>
internal static class PublicProfileEndpoints
{
    private const string _versionNote =
        "Send `If-Match` from the last read to get 409 PERSON_CONCURRENCY_CONFLICT if the person changed since. Answers the " +
        "person as GET /people/{personId} does; its version moves when the flag changes.";

    public static void Map(IEndpointRouteBuilder people)
    {
        people
            .MapPost("/{personId:guid}/public-profile/enable", (
                    Guid personId,
                    EnablePublicProfileHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, ifMatch, cancellationToken)))
            .WithName("EnablePersonPublicProfile")
            .WithSummary("Turn on a person's public profile")
            .WithDescription(
                "Turns on the person's public profile, so GET /api/public/v1/people/{personId} answers them while they are " +
                "active. A person who is a minor or whose age is unknown on the tenant's date needs an active guardian's " +
                "consent with `PUBLIC_PROFILE` (PERSON_CONSENT_REQUIRED); the public read checks the consent again on every " +
                "request, so withdrawing it hides the person without turning the profile off. An archived or deceased " +
                "person is refused (PERSON_ARCHIVED, PERSON_DECEASED). A profile already on is answered unchanged. " +
                _versionNote)
            .RequireAction(RegistryActions.PersonPublicProfileEnable)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.PersonConcurrencyConflict, RegistryErrors.ConsentRequired,
                RegistryErrors.PersonArchived, RegistryErrors.PersonDeceased);

        people
            .MapPost("/{personId:guid}/public-profile/disable", (
                    Guid personId,
                    DisablePublicProfileHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, ifMatch, cancellationToken)))
            .WithName("DisablePersonPublicProfile")
            .WithSummary("Turn off a person's public profile")
            .WithDescription(
                "Turns off the person's public profile, whatever their status: the public read answers PERSON_NOT_FOUND " +
                "at once (HTTP caches may keep it up to a minute, its photo up to five). A profile already off is answered " +
                $"unchanged. {_versionNote}")
            .RequireAction(RegistryActions.PersonPublicProfileDisable)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(RegistryErrors.PersonNotFound, RegistryErrors.PersonConcurrencyConflict);
    }
}
