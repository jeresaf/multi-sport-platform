using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.Photo;

/// <summary>
/// <c>PUT /people/{personId}/photo</c> (multipart, one file) and <c>DELETE /people/{personId}/photo</c> (S06 D19). The
/// form is not checked by ASP.NET Core's antiforgery: like every state-changing admin request it needs <c>X-CSRF: 1</c>.
/// </summary>
internal static class PersonPhotoEndpoints
{
    /// <summary>The form field that carries the photo.</summary>
    public const string FileField = PersonMedia.FileField;

    private const string _versionNote =
        "Send `If-Match` from the last read to get 409 PERSON_CONCURRENCY_CONFLICT if the person changed since. Answers the " +
        "person as GET /people/{personId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder people)
    {
        people
            .MapPut("/{personId:guid}/photo", SetAsync)
            .WithName("SetPersonPhoto")
            .WithSummary("Set a person's photo")
            .WithDescription(
                $"Uploads a photo as `multipart/form-data` with one file in the field `{FileField}` and nothing else " +
                "(VALIDATION_INVALID_REQUEST otherwise). The file's bytes decide its type, not the content type sent: JPEG, " +
                "PNG or WebP up to 5 MB (MEDIA_TYPE_NOT_ALLOWED, MEDIA_TOO_LARGE; an image that can't be decoded or is " +
                "over 40 megapixels is MEDIA_IMAGE_UNREADABLE). It is stored as WebP at 160, 640 and 1600 px on the " +
                "longest side, without its metadata (a GPS position, the camera), and replaces the photo the person had. " +
                "At the plan's storage limit the upload is ENTITLEMENT_QUOTA_EXCEEDED (\"Your plan allows N GB of " +
                "storage.\"). An archived or deceased person takes no new photo (PERSON_ARCHIVED, PERSON_DECEASED). " +
                _versionNote)
            .RequireAction(RegistryActions.PersonPhotoSet)
            .DisableAntiforgery()
            .WithMetadata(new StoresMediaMetadata())
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.PersonArchived,
                RegistryErrors.PersonDeceased, CommonErrors.InvalidRequest);

        people
            .MapDelete("/{personId:guid}/photo", RemoveAsync)
            .WithName("RemovePersonPhoto")
            .WithSummary("Remove a person's photo")
            .WithDescription(
                "Removes the person's photo, whatever the person's status; a person without one is PERSON_PHOTO_NOT_FOUND. " +
                _versionNote)
            .RequireAction(RegistryActions.PersonPhotoRemove)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.PhotoNotFound);
    }

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> SetAsync(
        Guid personId,
        IFormFile? file,
        SetPersonPhotoHandler handler,
        GetPersonHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (PersonMedia.RefuseUnlessOneFile(file, httpRequest) is { } malformed)
        {
            return malformed.ToProblem();
        }

        return await PersonChanges.AnswerAsync(personId, read, httpRequest, response, async ifMatch =>
        {
            await using var content = file!.OpenReadStream();
            return await handler.HandleAsync(personId, content, ifMatch, cancellationToken);
        });
    }

    private static Task<Results<Ok<PersonResponse>, ProblemHttpResult>> RemoveAsync(
        Guid personId,
        RemovePersonPhotoHandler handler,
        GetPersonHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken) =>
        PersonChanges.AnswerAsync(personId, read, httpRequest, response,
            ifMatch => handler.HandleAsync(personId, ifMatch, cancellationToken));
}
