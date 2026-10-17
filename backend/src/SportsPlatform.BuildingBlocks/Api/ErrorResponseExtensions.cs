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
}
