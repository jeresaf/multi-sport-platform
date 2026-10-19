using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.HttpResults;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class StrictJsonBodyTests
{
    public enum Surface
    {
        NaturalGrass,
        ArtificialTurf,
    }

    // The Host's options (ApiConventions): web defaults, strict numbers, UPPER_SNAKE enums.
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        NumberHandling = JsonNumberHandling.Strict,
        Converters = { new JsonStringEnumConverter(MessageSerialization.EnumNamingPolicy, allowIntegerValues: false) },
    };

    [Fact]
    public void Reads_a_valid_body()
    {
        Read("""{"name":"Bukoto Community Stadium","capacity":3000,"floodlit":true,"surface":"ARTIFICIAL_TURF"}""", out var request)
            .ShouldBeNull();

        request.ShouldBe(new VenueRequest("Bukoto Community Stadium", 3000, true, Surface.ArtificialTurf));
    }

    [Fact]
    public void Missing_and_null_members_are_null()
    {
        Read("""{"name":null}""", out var request).ShouldBeNull();

        request.ShouldBe(new VenueRequest(null, null, null, null));
    }

    [Theory]
    [InlineData("""{"colour":"red"}""", "colour")]
    [InlineData("""{"capacity":"3000"}""", "capacity")] // a number sent as a string
    [InlineData("""{"capacity":3000.5}""", "capacity")]
    [InlineData("""{"capacity":9999999999}""", "capacity")]
    [InlineData("""{"floodlit":"yes"}""", "floodlit")]
    [InlineData("""{"surface":"CLAY"}""", "surface")]
    [InlineData("""{"surface":1}""", "surface")]
    [InlineData("""{"name":{"en":"Bukoto"}}""", "name")]
    [InlineData("""{"name":"Bukoto","name":"Kisaasi"}""", "name")] // sent twice
    public void An_unknown_or_wrongly_typed_member_is_named(string body, string field)
    {
        var problem = Read(body, out _).ShouldNotBeNull().ProblemDetails;

        problem.Extensions["code"].ShouldBe("VALIDATION_INVALID_REQUEST");
        Errors(problem).ShouldBe([new FieldError(field, "VALIDATION_INVALID_REQUEST")]);
    }

    [Fact]
    public void A_member_is_named_as_the_request_names_it()
    {
        var problem = Read("""{"Capacity":"many"}""", out _).ShouldNotBeNull().ProblemDetails;

        Errors(problem).ShouldBe([new FieldError("capacity", "VALIDATION_INVALID_REQUEST")]);
    }

    [Fact]
    public void Every_bad_member_is_named_and_the_detail_does_not_echo_values()
    {
        var problem = Read("""{"colour":"red","capacity":"lots","floodlit":true}""", out _).ShouldNotBeNull().ProblemDetails;

        Errors(problem).ShouldBe([new FieldError("colour", "VALIDATION_INVALID_REQUEST"), new FieldError("capacity", "VALIDATION_INVALID_REQUEST")]);
        problem.Detail.ShouldNotBeNull().ShouldNotContain("lots");
        problem.Detail.ShouldNotContain("red");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"Bukoto\"")]
    [InlineData("null")]
    public void A_body_that_is_not_one_object_is_malformed(string body) =>
        Read(body, out _).ShouldNotBeNull().ProblemDetails.Extensions["code"].ShouldBe("VALIDATION_MALFORMED_REQUEST");

    private static ProblemHttpResult? Read(string body, out VenueRequest? request)
    {
        using var document = JsonDocument.Parse(body);
        var read = StrictJsonBody.TryRead(document.RootElement, _options, CommonErrors.InvalidRequest, out request, out var problem);
        read.ShouldBe(problem is null);
        return problem;
    }

    private static List<FieldError> Errors(Microsoft.AspNetCore.Mvc.ProblemDetails problem) =>
        ((IEnumerable<FieldError>)problem.Extensions["errors"].ShouldNotBeNull()).ToList();

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record VenueRequest(string? Name, int? Capacity, bool? Floodlit, Surface? Surface);
}
