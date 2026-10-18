using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>Turns <see cref="Result"/> failures into Problem Details; the Host adds <c>type</c> and <c>correlationId</c>.</summary>
public static class ResultHttpExtensions
{
    /// <summary>
    /// The error as Problem Details; <paramref name="detail"/> says what went wrong in this request (e.g. which
    /// parameter), never internal details.
    /// </summary>
    public static ProblemHttpResult ToProblem(this Error error, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return TypedResults.Problem(
            title: error.Message,
            detail: detail,
            statusCode: error.Kind.ToStatusCode(),
            extensions: new Dictionary<string, object?> { [ProblemFields.Code] = error.Code });
    }

    /// <summary>204 No Content on success, otherwise the error as Problem Details.</summary>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
    }
}
