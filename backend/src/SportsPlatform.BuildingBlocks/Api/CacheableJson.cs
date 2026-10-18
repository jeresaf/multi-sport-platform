using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>
/// A JSON body that HTTP caches may keep (docs/architecture/api-conventions.md, "Caching public reads"):
/// <c>Cache-Control: public, max-age=…</c> and a strong <c>ETag</c> (SHA-256 of the body). A request whose
/// <c>If-None-Match</c> matches gets <c>304 Not Modified</c> without a body. Nothing is cached on the server.
/// </summary>
public static class CacheableJson
{
    public static CacheableJson<T> Ok<T>(T value, TimeSpan maxAge) => new(value, maxAge);

    /// <summary>The strong entity tag of <paramref name="body"/>: the base64url SHA-256 of its bytes, quoted.</summary>
    public static EntityTagHeaderValue EntityTagOf(ReadOnlySpan<byte> body) =>
        new($"\"{Base64Url.EncodeToString(SHA256.HashData(body))}\"");

    /// <summary>
    /// Whether <paramref name="ifNoneMatch"/> matches <paramref name="entityTag"/> with weak comparison, as RFC 9110
    /// §13.1.2 requires (<c>*</c>, a list, <c>W/</c> tags). A header that doesn't parse matches nothing.
    /// </summary>
    public static bool Matches(StringValues ifNoneMatch, EntityTagHeaderValue entityTag)
    {
        ArgumentNullException.ThrowIfNull(entityTag);
        return !StringValues.IsNullOrEmpty(ifNoneMatch)
            && EntityTagHeaderValue.TryParseStrictList(ifNoneMatch, out var tags)
            && tags.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(entityTag, useStrongComparison: false));
    }
}

/// <summary>See <see cref="CacheableJson"/>. Documents <c>200</c> with <typeparamref name="T"/> and <c>304</c>.</summary>
public sealed class CacheableJson<T> : IResult, IStatusCodeHttpResult, IValueHttpResult<T>, IEndpointMetadataProvider
{
    internal CacheableJson(T value, TimeSpan maxAge)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAge, TimeSpan.Zero);
        Value = value;
        MaxAge = maxAge;
    }

    public T Value { get; }

    public TimeSpan MaxAge { get; }

    int? IStatusCodeHttpResult.StatusCode => StatusCodes.Status200OK;

    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "IEndpointMetadataProvider is a static abstract member.")]
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(T), ["application/json"]));
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status304NotModified, typeof(void)));
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var options = httpContext.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions
            ?? JsonSerializerOptions.Web;
        var body = JsonSerializer.SerializeToUtf8Bytes(Value, options);
        var entityTag = CacheableJson.EntityTagOf(body);

        var response = httpContext.Response;
        var headers = response.GetTypedHeaders();
        headers.ETag = entityTag;
        headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = MaxAge };

        if (CacheableJson.Matches(httpContext.Request.Headers.IfNoneMatch, entityTag))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength = body.Length;
        await response.Body.WriteAsync(body, httpContext.RequestAborted);
    }
}
