using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Host.Api;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>Fail closed at startup (S01 #22 N1): a validated request without a registered validator stops the Host.</summary>
public sealed class RequestValidatorRulesTests
{
    [Fact]
    public async Task Validated_request_without_a_validator_fails_the_startup_check()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapPost("/api/admin/v1/fixtures", (FixtureRequest request) => Results.Ok(request))
            .ValidateRequest<FixtureRequest>(CommonErrors.InvalidRequest);

        var exception = Should.Throw<InvalidOperationException>(app.EnsureRequestValidators);

        exception.Message.ShouldContain("/api/admin/v1/fixtures validates FixtureRequest, but no IValidator<FixtureRequest> is registered.");
    }

    [Fact]
    public async Task Validated_request_with_a_validator_passes()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<IValidator<FixtureRequest>, FixtureRequestValidator>();
        await using var app = builder.Build();
        app.MapPost("/api/admin/v1/fixtures", (FixtureRequest request) => Results.Ok(request))
            .ValidateRequest<FixtureRequest>(CommonErrors.InvalidRequest);

        Should.NotThrow(app.EnsureRequestValidators);
    }

    [Fact]
    public void The_Host_has_a_validator_for_every_validated_request()
    {
        using var factory = new HostOnlyApp();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        endpoints.ShouldContain(endpoint => endpoint.Metadata.GetMetadata<ValidatesRequestMetadata>() != null);
        RequestValidatorRules.Validate(endpoints, factory.Services.GetRequiredService<IServiceProviderIsService>()).ShouldBeEmpty();
    }

    public sealed record FixtureRequest(string? HomeTeam);

    private sealed class FixtureRequestValidator : AbstractValidator<FixtureRequest>
    {
        public FixtureRequestValidator() => RuleFor(request => request.HomeTeam).NotEmpty();
    }
}
