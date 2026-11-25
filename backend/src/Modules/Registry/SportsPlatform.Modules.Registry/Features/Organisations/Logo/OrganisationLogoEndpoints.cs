using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

namespace SportsPlatform.Modules.Registry.Features.Organisations.Logo;

/// <summary>
/// <c>PUT /organisations/{organisationId}/logo</c> (multipart, one file) and <c>DELETE /organisations/{organisationId}/logo</c>
/// (S07 D15). Like every state-changing admin request the form needs <c>X-CSRF: 1</c>, not ASP.NET Core's antiforgery.
/// </summary>
internal static class OrganisationLogoEndpoints
{
    private const string _versionNote =
        "Send `If-Match` from the last read to get 409 ORGANISATION_CONCURRENCY_CONFLICT if the organisation changed since. " +
        "Answers the organisation as GET /organisations/{organisationId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder organisations)
    {
        organisations
            .MapPut("/{organisationId:guid}/logo", SetAsync)
            .WithName("SetOrganisationLogo")
            .WithSummary("Set an organisation's logo")
            .WithDescription(
                RegistryMedia.ImageUploadNote("logo") +
                "A dissolved or archived organisation takes no new logo (ORGANISATION_DISSOLVED, ORGANISATION_ARCHIVED). " +
                _versionNote)
            .RequireAction(RegistryActions.OrganisationLogoSet)
            .DisableAntiforgery()
            .WithMetadata(new StoresMediaMetadata())
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(
                RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict,
                RegistryErrors.OrganisationArchived, RegistryErrors.OrganisationDissolved, CommonErrors.InvalidRequest);

        organisations
            .MapDelete("/{organisationId:guid}/logo", RemoveAsync)
            .WithName("RemoveOrganisationLogo")
            .WithSummary("Remove an organisation's logo")
            .WithDescription(
                "Removes the organisation's logo, whatever its status; an organisation without one is " +
                "ORGANISATION_LOGO_NOT_FOUND. " + _versionNote)
            .RequireAction(RegistryActions.OrganisationLogoRemove)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(
                RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict,
                RegistryErrors.OrganisationLogoNotFound);
    }

    private static async Task<Results<Ok<OrganisationResponse>, ProblemHttpResult>> SetAsync(
        Guid organisationId,
        IFormFile? file,
        SetOrganisationLogoHandler handler,
        GetOrganisationHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (RegistryMedia.RefuseUnlessOneFile(file, httpRequest) is { } malformed)
        {
            return malformed.ToProblem();
        }

        return await OrganisationChanges.AnswerAsync(organisationId, read, httpRequest, response, async ifMatch =>
        {
            await using var content = file!.OpenReadStream();
            return await handler.HandleAsync(organisationId, content, ifMatch, cancellationToken);
        });
    }

    private static Task<Results<Ok<OrganisationResponse>, ProblemHttpResult>> RemoveAsync(
        Guid organisationId,
        RemoveOrganisationLogoHandler handler,
        GetOrganisationHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken) =>
        OrganisationChanges.AnswerAsync(organisationId, read, httpRequest, response,
            ifMatch => handler.HandleAsync(organisationId, ifMatch, cancellationToken));
}
