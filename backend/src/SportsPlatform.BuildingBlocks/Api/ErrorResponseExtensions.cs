using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>The errors an endpoint can return; the Host lists their codes in the OpenAPI document.</summary>
public sealed record DeclaredErrorsMetadata(IReadOnlyList<Error> Errors);

/// <summary>Documents an endpoint's expected failures (docs/architecture/api-conventions.md, "OpenAPI and clients").</summary>
public static class ErrorResponseExtensions
{
    public const string ProblemContentType = "application/problem+json";

    /// <summary>
    /// One Problem Details response per HTTP status the <paramref name="errors"/> map to; the OpenAPI document lists
    /// each status's error codes in the response description.
    /// </summary>
    public static TBuilder ProducesErrors<TBuilder>(this TBuilder builder, params Error[] errors)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Length == 0)
        {
            throw new ArgumentException("Declare at least one error.", nameof(errors));
        }

        foreach (var status in errors.Select(error => error.Kind.ToStatusCode()).Distinct())
        {
            builder.WithMetadata(new ProducesResponseTypeMetadata(status, typeof(ProblemDetails), [ProblemContentType]));
        }

        return builder.WithMetadata(new DeclaredErrorsMetadata(errors));
    }

    /// <summary>
    /// Documents <paramref name="statusCode"/>'s Problem Details as <typeparamref name="TProblem"/>: a
    /// <see cref="ProblemDetails"/> subtype naming the extension members one of that status's errors carries (S07 D13's
    /// <c>candidates</c>). Call it after <see cref="ProducesErrors"/>; it replaces that status's plain schema.
    /// </summary>
    public static RouteHandlerBuilder ProducesProblem<TProblem>(this RouteHandlerBuilder builder, int statusCode)
        where TProblem : ProblemDetails
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Add(endpoint =>
        {
            foreach (var plain in endpoint.Metadata.OfType<ProducesResponseTypeMetadata>().Where(m => m.StatusCode == statusCode).ToList())
            {
                endpoint.Metadata.Remove(plain);
            }

            endpoint.Metadata.Add(new ProducesResponseTypeMetadata(statusCode, typeof(TProblem), [ProblemContentType]));
        });
        return builder;
    }
}
