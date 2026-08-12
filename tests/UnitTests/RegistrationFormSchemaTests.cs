using System.Text.Json;
using FluentAssertions;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Registrations.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class RegistrationFormSchemaTests
{
    [Fact]
    public void Normalize_SupportsEveryDocumentedFieldType()
    {
        var fields = new[]
        {
            Field("name", "text", required: true),
            Field("age", "Number"),
            Field("birth", "Date"),
            Field("division", "Select", options: [Option("open"), Option("elite")]),
            Field("extras", "Checkbox", options: [Option("meal")]),
            new RegistrationFormField { Id = "proof", FieldType = "File", Label = "Proof" },
            new RegistrationFormField { Id = "consent", FieldType = "Consent", Label = "Consent", ConsentText = "I accept." }
        };

        var normalized = RegistrationFormSchema.Normalize(fields);

        normalized.Select(field => field.FieldType).Should().Equal(
            "Text", "Number", "Date", "Select", "Checkbox", "File", "Consent");
        normalized.Single(field => field.Id == "proof").MaxFileSizeBytes.Should().Be(5 * 1024 * 1024);
        normalized.Single(field => field.Id == "proof").AllowedContentTypes.Should().Contain("application/pdf");
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown-condition")]
    [InlineData("missing-options")]
    [InlineData("invalid-regex")]
    public void Normalize_RejectsInvalidSchemas(string scenario)
    {
        var fields = scenario switch
        {
            "duplicate" => new[] { Field("name", "Text"), Field("NAME", "Text") },
            "unknown-condition" => new[] { Field("name", "Text"), new RegistrationFormField { Id = "detail", Label = "Detail", FieldType = "Text", ConditionalFieldId = "missing", ConditionalValue = "yes" } },
            "missing-options" => new[] { Field("division", "Select") },
            _ => new[] { new RegistrationFormField { Id = "name", Label = "Name", FieldType = "Text", ValidationRegex = "[" } }
        };

        var action = () => RegistrationFormSchema.Normalize(fields);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_AppliesRulesOnlyWhenConditionalFieldIsVisible()
    {
        var fields = RegistrationFormSchema.Normalize([
            Field("division", "Select", required: true, options: [Option("open"), Option("elite")]),
            new RegistrationFormField { Id = "ranking", FieldType = "Number", Label = "Ranking", IsRequired = true, MinValue = 1, ConditionalFieldId = "division", ConditionalValue = "elite" },
            new RegistrationFormField { Id = "consent", FieldType = "Consent", Label = "Consent", ConsentText = "I accept.", IsRequired = true }
        ]);
        var schema = JsonSerializer.Serialize(fields);

        var open = RegistrationFormSchema.Validate(schema, new Dictionary<string, object>
        {
            ["division"] = "open", ["consent"] = true
        });
        var elite = RegistrationFormSchema.Validate(schema, new Dictionary<string, object>
        {
            ["division"] = "elite", ["consent"] = true
        });

        open.IsValid.Should().BeTrue();
        elite.IsValid.Should().BeFalse();
        elite.Errors.Should().ContainSingle(error => error.Contains("Ranking"));
    }

    [Fact]
    public void Submission_PinsSchemaVersionAndTracksDraftRevisions()
    {
        var form = new RegistrationForm(Guid.NewGuid(), Guid.NewGuid(), "Player entry", "owner");
        form.UpdateSchema("[{\"id\":\"name\"}]");
        var submission = new RegistrationFormSubmission(
            Guid.NewGuid(), form.Id, Guid.NewGuid(), form.Version, form.FormSchema);

        form.UpdateSchema("[{\"id\":\"changed\"}]");
        submission.UpdateFormData("{\"name\":\"Ada\"}");

        submission.FormVersion.Should().Be(2);
        submission.FormSchemaSnapshot.Should().Contain("name").And.NotContain("changed");
        submission.Revision.Should().Be(2);
    }

    private static RegistrationFormField Field(
        string id,
        string type,
        bool required = false,
        List<SelectOption>? options = null) => new()
        {
            Id = id,
            FieldType = type,
            Label = id,
            IsRequired = required,
            Options = options
        };

    private static SelectOption Option(string value) => new() { Value = value, Label = value };
}
