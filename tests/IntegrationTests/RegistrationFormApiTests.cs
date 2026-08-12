using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class RegistrationFormApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid CompetitionId = Guid.Parse("00000000-0000-0000-0000-000000000300");
    private static readonly Guid ParticipantId = Guid.Parse("00000000-0000-0000-0000-000000000200");
    private readonly ApiWebApplicationFactory _factory;

    public RegistrationFormApiTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task VersionedForms_RenderValidateUploadAndPinSubmissionSchema()
    {
        using var owner = _factory.CreateClient();
        await AuthorizeAsync(owner, "admin@premier.com", "TenantAdmin123!", "registration-owner");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var create = await owner.PostAsJsonAsync("/api/registrationforms", new
        {
            tenantId = TenantId,
            competitionId = CompetitionId,
            name = $"Player registration {suffix}",
            description = "Versioned acceptance form",
            createdBy = "untrusted-client-value"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var formId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var fields = SchemaFields();
        var schemaUpdate = await owner.PutAsJsonAsync(
            $"/api/registrationforms/{formId}/schema", new { fields });
        schemaUpdate.StatusCode.Should().Be(HttpStatusCode.OK,
            await schemaUpdate.Content.ReadAsStringAsync());

        var schema = await owner.GetFromJsonAsync<JsonElement>(
            $"/api/registrationforms/{formId}/schema");
        schema.GetProperty("version").GetInt32().Should().Be(2);
        schema.GetProperty("fields").GetArrayLength().Should().Be(8);

        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 registration proof"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using var uploadBody = new MultipartFormDataContent();
        uploadBody.Add(file, "file", "proof.pdf");
        var upload = await owner.PostAsync(
            $"/api/registrationforms/{formId}/fields/proof/files", uploadBody);
        upload.StatusCode.Should().Be(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync());
        var uploaded = await upload.Content.ReadFromJsonAsync<JsonElement>();
        uploaded.GetProperty("fileName").GetString().Should().Be("proof.pdf");
        uploaded.GetProperty("contentType").GetString().Should().Be("application/pdf");
        var downloadPath = new Uri(uploaded.GetProperty("url").GetString()!).PathAndQuery;
        (await owner.GetAsync(downloadPath)).StatusCode.Should().Be(HttpStatusCode.OK);

        var hiddenCondition = await owner.PostAsJsonAsync(
            $"/api/registrationforms/{formId}/validate", new
            {
                formData = new Dictionary<string, object>
                {
                    ["name"] = "Ada Player", ["division"] = "open", ["consent"] = true,
                    ["proof"] = uploaded
                }
            });
        var hiddenResult = await hiddenCondition.Content.ReadFromJsonAsync<JsonElement>();
        hiddenResult.GetProperty("isValid").GetBoolean().Should().BeTrue();

        var visibleCondition = await owner.PostAsJsonAsync(
            $"/api/registrationforms/{formId}/validate", new
            {
                formData = new Dictionary<string, object>
                {
                    ["name"] = "Ada Player", ["division"] = "elite", ["consent"] = true,
                    ["proof"] = uploaded
                }
            });
        var visibleResult = await visibleCondition.Content.ReadFromJsonAsync<JsonElement>();
        visibleResult.GetProperty("isValid").GetBoolean().Should().BeFalse();
        visibleResult.GetProperty("errors").GetRawText().Should().Contain("Ranking");

        var submissionCreate = await owner.PostAsJsonAsync(
            "/api/registrationforms/submissions", new
            {
                formId, participantId = ParticipantId, contestId = (Guid?)null
            });
        submissionCreate.StatusCode.Should().Be(HttpStatusCode.Created,
            await submissionCreate.Content.ReadAsStringAsync());
        var submissionId = (await submissionCreate.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        fields[0]["label"] = "Updated player name";
        var nextSchema = await owner.PutAsJsonAsync(
            $"/api/registrationforms/{formId}/schema", new { fields });
        nextSchema.StatusCode.Should().Be(HttpStatusCode.OK,
            await nextSchema.Content.ReadAsStringAsync());

        var formData = new Dictionary<string, object>
        {
            ["name"] = "Ada Player",
            ["age"] = 21,
            ["birth_date"] = "2005-01-02",
            ["division"] = "elite",
            ["extras"] = new[] { "meal" },
            ["proof"] = uploaded,
            ["consent"] = true,
            ["ranking"] = 4
        };
        var updateData = await owner.PutAsJsonAsync(
            $"/api/registrationforms/submissions/{submissionId}/data", new { formData });
        updateData.StatusCode.Should().Be(HttpStatusCode.OK,
            await updateData.Content.ReadAsStringAsync());
        var submit = await owner.PostAsync(
            $"/api/registrationforms/submissions/{submissionId}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());

        var submission = await owner.GetFromJsonAsync<JsonElement>(
            $"/api/registrationforms/submissions/{submissionId}");
        submission.GetProperty("formVersion").GetInt32().Should().Be(2);
        submission.GetProperty("revision").GetInt32().Should().Be(2);
        submission.GetProperty("formSchemaSnapshot").GetString()
            .Should().Contain("Player name").And.NotContain("Updated player name");

        using var anonymous = _factory.CreateClient();
        var publicResponse = await anonymous.GetAsync(
            $"/api/registrationforms/public/competition/{CompetitionId}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        publicResponse.Headers.CacheControl!.Public.Should().BeTrue();
        var publicForms = await publicResponse.Content.ReadFromJsonAsync<JsonElement>();
        var publicForm = publicForms.EnumerateArray().Single(item =>
            item.GetProperty("id").GetGuid() == formId);
        publicForm.GetProperty("version").GetInt32().Should().Be(3);
        publicForm.GetProperty("fields").GetArrayLength().Should().Be(8);

        using var scorer = _factory.CreateClient();
        await AuthorizeAsync(scorer, "scorer@premier.com", "Scorer123!", "registration-scorer");
        (await scorer.PostAsJsonAsync("/api/registrationforms", new
        {
            tenantId = TenantId, competitionId = CompetitionId, name = "Forbidden form"
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        (await database.AuditEntries.CountAsync(item =>
            item.EntityId == formId.ToString() &&
            (item.Action == "RegistrationFormCreated" ||
             item.Action == "RegistrationFormSchemaUpdated")))
            .Should().Be(3);
        database.RegistrationFormSubmissions.RemoveRange(
            database.RegistrationFormSubmissions.Where(item => item.FormId == formId));
        database.RegistrationForms.RemoveRange(
            database.RegistrationForms.Where(item => item.Id == formId));
        await database.SaveChangesAsync();
    }

    private static List<Dictionary<string, object?>> SchemaFields() =>
    [
        Field("name", "Text", "Player name", true, 0, new() { ["minLength"] = 2, ["validationRegex"] = "^[A-Za-z ]+$" }),
        Field("age", "Number", "Age", false, 1, new() { ["minValue"] = 5, ["maxValue"] = 100 }),
        Field("birth_date", "Date", "Birth date", false, 2),
        Field("division", "Select", "Division", true, 3, new() { ["options"] = Options("open", "elite") }),
        Field("extras", "Checkbox", "Extras", false, 4, new() { ["options"] = Options("meal", "transport") }),
        Field("proof", "File", "Proof", true, 5, new() { ["allowedContentTypes"] = new[] { "application/pdf" }, ["maxFileSizeBytes"] = 1024 * 1024 }),
        Field("consent", "Consent", "Consent", true, 6, new() { ["consentText"] = "I accept the competition rules." }),
        Field("ranking", "Number", "Ranking", true, 7, new() { ["minValue"] = 1, ["conditionalFieldId"] = "division", ["conditionalValue"] = "elite" })
    ];

    private static Dictionary<string, object?> Field(
        string id, string fieldType, string label, bool required, int order,
        Dictionary<string, object?>? additions = null)
    {
        var field = new Dictionary<string, object?>
        {
            ["id"] = id, ["fieldType"] = fieldType, ["label"] = label,
            ["isRequired"] = required, ["order"] = order, ["showInList"] = true
        };
        if (additions is not null)
            foreach (var (key, value) in additions) field[key] = value;
        return field;
    }

    private static object[] Options(params string[] values) => values
        .Select(value => (object)new { value, label = value }).ToArray();

    private static async Task AuthorizeAsync(
        HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Registration form acceptance test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
