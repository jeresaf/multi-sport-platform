using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>One entry of a validation problem's <c>errors</c> (docs/architecture/api-conventions.md).</summary>
public sealed record FieldError(string Field, string Code);

/// <summary>
/// Marks an endpoint that validates <paramref name="RequestType"/> (<c>.ValidateRequest&lt;T&gt;()</c>), so the Host can
/// check at startup that an <c>IValidator&lt;T&gt;</c> is registered for it.
/// </summary>
public sealed record ValidatesRequestMetadata(Type RequestType);

/// <summary>
/// Checks a request's shape with its FluentValidation validator (<c>IValidator&lt;TRequest&gt;</c>, registered by the
/// module) before the handler runs. Authorization runs earlier, so a caller without the action never learns how a
/// request is validated.
/// </summary>
public static class RequestValidationExtensions
{
    /// <summary>
    /// Validates the endpoint's <typeparamref name="TRequest"/> argument (a body or <c>[AsParameters]</c>); a failure
    /// is <paramref name="error"/> as Problem Details, with the messages as <c>detail</c> and one <c>errors</c> entry per
    /// failure. A rule's <c>WithErrorCode</c> sets the entry's code when it is an <c>AREA_CONDITION</c> code; otherwise
    /// the entry carries <paramref name="error"/>'s code. Also documents <paramref name="error"/> in OpenAPI.
    /// </summary>
    public static RouteHandlerBuilder ValidateRequest<TRequest>(this RouteHandlerBuilder builder, Error error)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(error);

        builder.WithMetadata(new ValidatesRequestMetadata(typeof(TRequest)));
        builder.AddEndpointFilter(async (context, next) =>
        {
            // A required body that wasn't sent fails binding, but filters still run (with a null argument) before the
            // pipeline answers 400 VALIDATION_MALFORMED_REQUEST: there is nothing to validate.
            if (context.Arguments.OfType<TRequest>().SingleOrDefault() is not { } request)
            {
                return await next(context);
            }

            return await context.HttpContext.ValidateRequestAsync(request, error) ?? await next(context);
        });
        return builder.ProducesErrors(error);
    }

    /// <summary>
    /// What <see cref="ValidateRequest{TRequest}"/> does, for a request the endpoint reads itself (e.g. through
    /// <see cref="StrictJsonBody"/>): null when valid, otherwise <paramref name="error"/> as Problem Details.
    /// </summary>
    public static async Task<ProblemHttpResult?> ValidateRequestAsync<TRequest>(this HttpContext context, TRequest request, Error error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        var validator = context.RequestServices.GetRequiredService<IValidator<TRequest>>();
        var result = await validator.ValidateAsync(request, context.RequestAborted);
        return result.IsValid ? null : result.ToProblem(error);
    }

    public static ProblemHttpResult ToProblem(this ValidationResult result, Error error)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(error);

        var problem = error.ToProblem(string.Join(" ", result.Errors.Select(failure => failure.ErrorMessage)));
        problem.ProblemDetails.Extensions[ProblemFields.Errors] = result.Errors
            .Select(failure => new FieldError(
                JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName),
                Error.IsCode(failure.ErrorCode) ? failure.ErrorCode : error.Code))
            .ToList();
        return problem;
    }
}
