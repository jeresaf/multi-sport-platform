using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class ResultHttpExtensionsTests
{
    private static readonly Error _venueClash = new("FIXTURE_VENUE_CLASH", "The venue is already booked.", ErrorKind.Conflict);

    public static TheoryData<ErrorKind, int> StatusByKind => new()
    {
        { ErrorKind.Validation, 400 },
        { ErrorKind.Unauthorized, 401 },
        { ErrorKind.Forbidden, 403 },
        { ErrorKind.NotFound, 404 },
        { ErrorKind.MethodNotAllowed, 405 },
        { ErrorKind.Conflict, 409 },
        { ErrorKind.UnsupportedMediaType, 415 },
        { ErrorKind.Unprocessable, 422 },
        { ErrorKind.TooManyRequests, 429 },
        { ErrorKind.Unexpected, 500 },
        { ErrorKind.Unavailable, 503 },
    };

    [Theory]
    [MemberData(nameof(StatusByKind))]
    public void Each_error_kind_maps_to_its_HTTP_status(ErrorKind kind, int status) => kind.ToStatusCode().ShouldBe(status);

    [Fact]
    public void Every_error_kind_has_a_status() =>
        StatusByKind.Select(row => (ErrorKind)row.Data.Item1).ShouldBe(Enum.GetValues<ErrorKind>(), ignoreOrder: true);

    [Fact]
    public void ToProblem_carries_status_title_and_code()
    {
        var problem = _venueClash.ToProblem().ProblemDetails;

        problem.Status.ShouldBe(StatusCodes.Status409Conflict);
        problem.Title.ShouldBe("The venue is already booked.");
        problem.Extensions[ProblemFields.Code].ShouldBe("FIXTURE_VENUE_CLASH");
    }

    [Fact]
    public void Successful_result_is_no_content() => Result.Success().ToHttpResult().ShouldBeOfType<NoContent>();

    [Fact]
    public void Failed_result_is_a_problem() =>
        Result.Failure(_venueClash).ToHttpResult().ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(409);

    [Fact]
    public void Successful_result_with_a_value_uses_the_success_mapping()
    {
        var http = Result.Success(42).ToHttpResult(value => TypedResults.Ok(value));

        http.ShouldBeOfType<Ok<int>>().Value.ShouldBe(42);
    }

    [Fact]
    public void Failed_result_with_a_value_skips_the_success_mapping()
    {
        var http = Result.Failure<int>(_venueClash).ToHttpResult(_ => throw new InvalidOperationException("Must not run."));

        http.ShouldBeOfType<ProblemHttpResult>().ProblemDetails.Extensions[ProblemFields.Code].ShouldBe("FIXTURE_VENUE_CLASH");
    }
}
