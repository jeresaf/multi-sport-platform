using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SportsPlatform.Api.Middleware;
using SportsPlatform.Api.Services;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class ErrorReportingTests
{
    [Fact]
    public async Task UnhandledFailure_IsReportedOnceWithSafeCorrelationContext()
    {
        var reporter = new Mock<IErrorReporter>();
        ErrorReportContext? captured = null;
        reporter
            .Setup(value => value.ReportAsync(
                It.IsAny<Exception>(),
                It.IsAny<ErrorReportContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<Exception, ErrorReportContext, CancellationToken>((_, context, _) => captured = context)
            .Returns(Task.CompletedTask);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns("Production");
        var services = new ServiceCollection()
            .AddSingleton(reporter.Object)
            .AddSingleton(environment.Object)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/test-failure";
        httpContext.Items[CorrelationContextMiddleware.ItemName] = "test-correlation";

        var middleware = new ErrorHandlingMiddleware(
            _ => throw new InvalidDataException("sensitive internal detail"),
            NullLogger<ErrorHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        captured.Should().NotBeNull();
        captured!.CorrelationId.Should().Be("test-correlation");
        captured.Method.Should().Be(HttpMethods.Post);
        captured.Path.Should().Be("/api/test-failure");
        reporter.Verify(value => value.ReportAsync(
            It.IsAny<InvalidDataException>(),
            It.IsAny<ErrorReportContext>(),
            It.IsAny<CancellationToken>()), Times.Once);

        httpContext.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(httpContext.Response.Body);
        response.RootElement.GetProperty("detail").GetString()
            .Should().Be("An unexpected error occurred. Please try again later.");
        response.RootElement.GetProperty("correlationId").GetString().Should().Be("test-correlation");
        response.RootElement.ToString().Should().NotContain("sensitive internal detail");
    }
}
