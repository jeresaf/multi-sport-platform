using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Host.Api;

/// <summary>
/// The error contract (docs/architecture/api-conventions.md): every error response is Problem Details with a stable
/// <c>code</c>, a <c>type</c> under <c>ProblemDetails:TypeBaseUri</c> and the request's <c>correlationId</c>.
/// </summary>
public static class ApiConventions
{
    public const string TypeBaseUriSetting = "ProblemDetails:TypeBaseUri";

    public static IServiceCollection AddApiConventions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);

        var typeBaseUri = configuration[TypeBaseUriSetting];
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context => Customize(context, typeBaseUri));

        // Binding failures throw BadHttpRequestException when RouteHandlerOptions.ThrowOnBadRequest is on (Development).
        services.Configure<ExceptionHandlerOptions>(options => options.StatusCodeSelector = exception =>
            exception is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError);

        return services;
    }

    /// <summary>Correlation ID first, so every later response (errors included) carries it.</summary>
    public static IApplicationBuilder UseApiConventions(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    private static void Customize(ProblemDetailsContext context, string? typeBaseUri)
    {
        var problem = context.ProblemDetails;

        // Problems from the framework (routing, binding, unhandled exceptions) have no code yet: use the pipeline's.
        if (!problem.Extensions.ContainsKey(ProblemFields.Code)
            && PipelineError(problem.Status ?? context.HttpContext.Response.StatusCode, context.HttpContext) is { } error)
        {
            problem.Title = error.Message;
            problem.Detail = null;
            problem.Extensions[ProblemFields.Code] = error.Code;
        }

        if (typeBaseUri is not null && problem.Extensions.TryGetValue(ProblemFields.Code, out var code) && code is string)
        {
            problem.Type = typeBaseUri + code;
        }

        // One request ID in the body: the framework's traceId would differ from correlationId once tracing is on.
        problem.Extensions.Remove("traceId");
        problem.Extensions[ProblemFields.CorrelationId] = context.HttpContext.TraceIdentifier;
    }

    // Other statuses get their codes from the code that produces them (authentication and authorization: T4, S03).
    // A bare 404 from a matched endpoint is a bug (endpoints return ToProblem()); it must not claim "no route".
    private static Error? PipelineError(int status, HttpContext httpContext) => status switch
    {
        StatusCodes.Status400BadRequest => CommonErrors.MalformedRequest,
        StatusCodes.Status404NotFound when httpContext.GetEndpoint() is null => CommonErrors.RouteNotFound,
        StatusCodes.Status405MethodNotAllowed => CommonErrors.MethodNotAllowed,
        StatusCodes.Status415UnsupportedMediaType => CommonErrors.UnsupportedMediaType,
        StatusCodes.Status500InternalServerError => CommonErrors.UnexpectedError,
        _ => null,
    };
}
