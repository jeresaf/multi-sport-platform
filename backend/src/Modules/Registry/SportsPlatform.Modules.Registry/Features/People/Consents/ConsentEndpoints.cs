using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.Consents;

/// <summary>
/// <c>POST /people/{personId}/consents</c>, <c>POST /people/{personId}/consents/{consentId}/withdraw</c> and
/// <c>PUT /people/{personId}/consents/{consentId}/evidence</c> (multipart, one file) (S06 D14, D19). A consent is
/// recorded first and its evidence uploaded next: JSON bodies stay JSON.
/// </summary>
internal static class ConsentEndpoints
{
    private const string _versionNote =
        "Send `version` or `If-Match` from the last read to get 409 PERSON_CONCURRENCY_CONFLICT if the person changed since; " +
        "without them, a change to the same person saved at the same moment can still answer it. " +
        "Answers the person as GET /people/{personId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder people)
    {
        people
            .MapPost("/{personId:guid}/consents", (
                    Guid personId,
                    RecordConsentRequest request,
                    RecordConsentHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, request, ifMatch, cancellationToken)))
            .WithName("RecordPersonConsent")
            .WithSummary("Record a guardian's consent")
            .WithDescription(
                "Records a guardian's consent for a person who is a minor or whose age is unknown on the tenant's date: the " +
                "guardian's name, relationship and contact (a phone number or an email address), the scopes " +
                "(`DATA_PROCESSING`, `PUBLIC_PROFILE`, `PHOTO`) and the date given, neither after today nor before the " +
                "date of birth. Several consents may be active at once (one per guardian, say); the person's " +
                "`activeConsentScopes` are theirs together. The guardian's details are private: only GET " +
                "/people/{personId}/private shows them. An adult is PERSON_CONSENT_NOT_NEEDED; an archived or deceased " +
                $"person takes no new consent (PERSON_ARCHIVED, PERSON_DECEASED). Upload the evidence next. {_versionNote}")
            .RequireAction(RegistryActions.PersonConsentRecord)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<RecordConsentRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.ConsentNotNeeded,
                RegistryErrors.PersonArchived, RegistryErrors.PersonDeceased);

        people
            .MapPost("/{personId:guid}/consents/{consentId:guid}/withdraw", (
                    Guid personId,
                    Guid consentId,
                    WithdrawConsentRequest request,
                    WithdrawConsentHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, consentId, request, ifMatch, cancellationToken)))
            .WithName("WithdrawPersonConsent")
            .WithSummary("Withdraw scopes of a guardian's consent")
            .WithDescription(
                "Withdraws some or all of an active consent's scopes with a reason (body `{ \"scopes\": […], \"reason\": … }`, " +
                "both required), whatever the person's status or age. The consent is kept as `WITHDRAWN` with who withdrew " +
                "it, when and why; when scopes remain, a new active consent with them is recorded (the same guardian, date " +
                "and evidence, naming the one it replaces). A scope the consent doesn't give is VALIDATION_INVALID_REQUEST " +
                "on `scopes`; a consent already withdrawn is PERSON_CONSENT_WITHDRAWN. The reason is kept permanently and " +
                $"in the audit trail: don't include personal data. {_versionNote}")
            .RequireAction(RegistryActions.PersonConsentWithdraw)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<WithdrawConsentRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.ConsentNotFound,
                RegistryErrors.ConsentWithdrawn, CommonErrors.ReasonRequired);

        people
            .MapPut("/{personId:guid}/consents/{consentId:guid}/evidence", UploadEvidenceAsync)
            .WithName("UploadPersonConsentEvidence")
            .WithSummary("Upload the evidence of a guardian's consent")
            .WithDescription(
                $"Uploads the evidence of an active consent (a signed form, say) as `multipart/form-data` with one file in " +
                $"the field `{PersonMedia.FileField}` and nothing else (VALIDATION_INVALID_REQUEST otherwise). The file's " +
                "bytes decide its type, not the content type sent: PDF, JPEG or PNG up to 10 MB (MEDIA_TYPE_NOT_ALLOWED, " +
                "MEDIA_TOO_LARGE, MEDIA_IMAGE_UNREADABLE). A PDF is stored as uploaded; an image is re-encoded without " +
                "its metadata (a GPS position, the device). It replaces the evidence the consent had. At the plan's " +
                "storage limit the upload is ENTITLEMENT_QUOTA_EXCEEDED (\"Your plan allows N GB of storage.\"). A " +
                "withdrawn consent is PERSON_CONSENT_WITHDRAWN; an archived or deceased person takes no new evidence " +
                "(PERSON_ARCHIVED, PERSON_DECEASED). The evidence is private: only GET /people/{personId}/private links " +
                "it. Send `If-Match` from the last read to get 409 PERSON_CONCURRENCY_CONFLICT if the person changed " +
                "since. Answers the person as GET /people/{personId} does; its version moves.")
            .RequireAction(RegistryActions.PersonConsentEvidenceUpload)
            .DisableAntiforgery()
            .WithMetadata(new StoresMediaMetadata())
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.ConsentNotFound,
                RegistryErrors.ConsentWithdrawn, RegistryErrors.PersonArchived, RegistryErrors.PersonDeceased,
                CommonErrors.InvalidRequest);
    }

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> UploadEvidenceAsync(
        Guid personId,
        Guid consentId,
        IFormFile? file,
        UploadConsentEvidenceHandler handler,
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
            return await handler.HandleAsync(personId, consentId, content, ifMatch, cancellationToken);
        });
    }
}
