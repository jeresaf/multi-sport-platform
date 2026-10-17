using Microsoft.AspNetCore.Http;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>The HTTP status of each <see cref="ErrorKind"/> (docs/architecture/api-conventions.md, "HTTP mapping").</summary>
public static class ErrorHttpStatus
{
    public static int ToStatusCode(this ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.MethodNotAllowed => StatusCodes.Status405MethodNotAllowed,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
        ErrorKind.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.TooManyRequests => StatusCodes.Status429TooManyRequests,
        ErrorKind.Unexpected => StatusCodes.Status500InternalServerError,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown error kind."),
    };
}
