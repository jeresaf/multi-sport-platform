using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.People.GetPublicPerson;

/// <summary>
/// <c>GET /api/public/v1/people/{personId}</c> and <c>…/photo?variant=</c> (S06 D17, D18): the tenant comes from the
/// request host. Both check the person's exposure on every request; HTTP caches may keep the read for a minute and the
/// photo for five, so a withdrawn scope leaves edge caches within that time.
/// </summary>
internal static class PublicPersonEndpoints
{
    public const string Tag = "Public people";

    public const string RouteGroup = "/people";

    public static readonly TimeSpan ReadMaxAge = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan PhotoMaxAge = TimeSpan.FromMinutes(5);

    /// <summary>The photo route of a variant, relative to the API's origin.</summary>
    public static string PhotoPath(Guid personId, PublicPhotoVariant variant) =>
        $"/api/public/v1{RouteGroup}/{personId}/photo?variant={PublicPersonPhotoRequest.Name(variant)}";

    public static void Map(IEndpointRouteBuilder publicApi)
    {
        var people = publicApi.MapGroup(RouteGroup).WithTags(Tag);

        people
            .MapGet("/{personId:guid}", GetAsync)
            .WithName("GetPublicPerson")
            .WithSummary("Get a person on the public site")
            .WithDescription(
                "A person of the tenant whose site this host serves, when the person is active, their public profile is on " +
                "and, for a person who is a minor or whose age is unknown, a guardian's consent with `PUBLIC_PROFILE` is in " +
                "force; otherwise PERSON_NOT_FOUND. An adult shows nationality, the photo and active athlete profiles (codes; " +
                "labels from the sport's definition); anyone else only the display name, and the photo with `PHOTO` consent. " +
                "`photo` holds the photo route's paths. Cacheable for a minute; send the `ETag` back as `If-None-Match` to " +
                "get 304.")
            .RequirePublicTenant()
            .ProducesErrors(RegistryErrors.PersonNotFound);

        people
            .MapGet("/{personId:guid}/photo", GetPhotoAsync)
            .WithName("GetPublicPersonPhoto")
            .WithSummary("Get a person's photo on the public site")
            .WithDescription(
                "A variant (`THUMBNAIL` 160 px, `MEDIUM` 640 px, `LARGE` 1600 px on the longest side; required) of the photo " +
                "of a person the public read shows, as WebP. Checked as the public read is, on every request: a person it " +
                "doesn't show is PERSON_NOT_FOUND, one shown without a photo (none, or no `PHOTO` consent for a minor) " +
                "PERSON_PHOTO_NOT_FOUND. `Cache-Control: public, max-age=300`.")
            .RequirePublicTenant()
            .ValidateRequest<PublicPersonPhotoRequest>(CommonErrors.InvalidQuery)
            .DocumentsQueryValues("variant", PublicPersonPhotoRequest.Variants)
            .Produces<Stream>(StatusCodes.Status200OK, "image/webp")
            .ProducesErrors(RegistryErrors.PersonNotFound, RegistryErrors.PhotoNotFound);
    }

    private static async Task<Results<CacheableJson<PublicPersonResponse>, ProblemHttpResult>> GetAsync(
        Guid personId, GetPublicPersonHandler handler, CancellationToken cancellationToken)
    {
        var person = await handler.HandleAsync(personId, cancellationToken);
        if (person.IsFailure)
        {
            return person.Error.ToProblem();
        }

        return CacheableJson.Ok(person.Value, ReadMaxAge);
    }

    private static async Task<IResult> GetPhotoAsync(
        Guid personId,
        [AsParameters] PublicPersonPhotoRequest request,
        GetPublicPersonPhotoHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var opened = await handler.HandleAsync(personId, request.ToVariant(), cancellationToken);
        if (opened.IsFailure)
        {
            return opened.Error.ToProblem();
        }

        var content = opened.Value;
        httpContext.Response.RegisterForDisposeAsync(content);
        httpContext.Response.Headers.CacheControl =
            string.Create(CultureInfo.InvariantCulture, $"public, max-age={(int)PhotoMaxAge.TotalSeconds}");
        httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return Results.Stream(content.Content, content.ContentType);
    }
}
