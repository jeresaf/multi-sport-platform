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

public sealed class BulkImportWorkflowTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly ApiWebApplicationFactory _factory;
    public BulkImportWorkflowTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PreviewImportHistoryAndReport_IsolateInvalidAndDuplicateRows()
    {
        using var client = _factory.CreateClient(); await Authorize(client, "bulk-import-workflow");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var name = $"CSV Athlete {suffix}";
        var csv = "Name,ShortName,DateOfBirth,Gender,Nationality,Email,Phone,EmergencyContact,MedicalClearance\n"
            + $"{name},CA,2000-01-02,Female,Kenya,,,,true\n"
            + $"Broken {suffix},BA,wrong-date,Male,Kenya,,,,false\n"
            + $"{name},CA2,2001-03-04,Female,Kenya,,,,false\n";

        var preview = await client.PostAsync($"/api/bulkimport/preview/athletes?tenantId={TenantId}", Upload(csv));
        preview.StatusCode.Should().Be(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var previewJson = await preview.Content.ReadFromJsonAsync<JsonElement>();
        previewJson.GetProperty("total").GetInt32().Should().Be(3);
        previewJson.GetProperty("valid").GetInt32().Should().Be(1);
        previewJson.GetProperty("duplicates").GetInt32().Should().Be(1);
        previewJson.GetProperty("invalid").GetInt32().Should().Be(1);

        var imported = await client.PostAsync($"/api/bulkimport/athletes?tenantId={TenantId}&duplicateHandling=Skip", Upload(csv));
        imported.StatusCode.Should().Be(HttpStatusCode.OK, await imported.Content.ReadAsStringAsync());
        var details = await imported.Content.ReadFromJsonAsync<JsonElement>();
        var summary = details.GetProperty("summary");
        var jobId = summary.GetProperty("id").GetGuid();
        summary.GetProperty("importedRows").GetInt32().Should().Be(1);
        summary.GetProperty("duplicateRows").GetInt32().Should().Be(1);
        summary.GetProperty("failedRows").GetInt32().Should().Be(1);

        var report = await client.GetAsync($"/api/bulkimport/{jobId}/report");
        report.StatusCode.Should().Be(HttpStatusCode.OK);
        var reportText = await report.Content.ReadAsStringAsync();
        reportText.Should().Contain("Imported=1").And.Contain("DateOfBirth");
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/bulkimport/history?tenantId={TenantId}");
        history.EnumerateArray().Should().Contain(item => item.GetProperty("id").GetGuid() == jobId);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        database.Participants.Remove(await database.Participants.SingleAsync(item => item.Name == name));
        database.BulkImportJobs.Remove(await database.BulkImportJobs.SingleAsync(item => item.Id == jobId));
        await database.SaveChangesAsync();
    }

    [Fact]
    public async Task LargeImport_IsAcceptedAndCompletedByBackgroundWorker()
    {
        using var client = _factory.CreateClient(); await Authorize(client, "bulk-import-background");
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var csv = new StringBuilder("Name,ShortName,HomeVenue,Coach,Manager\n");
        for (var index = 1; index <= 100; index++) csv.Append($"Queue Team {suffix} {index},Q{index},Venue,,\n");
        var response = await client.PostAsync($"/api/bulkimport/teams?tenantId={TenantId}&duplicateHandling=Skip", Upload(csv.ToString()));
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        JsonElement job = default;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await Task.Delay(50);
            job = await client.GetFromJsonAsync<JsonElement>($"/api/bulkimport/{jobId}");
            if (job.GetProperty("summary").GetProperty("status").GetString() is "Completed" or "Failed") break;
        }
        var completed = job.GetProperty("summary");
        completed.GetProperty("status").GetString().Should().Be("Completed");
        completed.GetProperty("importedRows").GetInt32().Should().Be(100);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        database.Participants.RemoveRange(database.Participants.Where(item => item.Name.StartsWith($"Queue Team {suffix} ")));
        database.BulkImportJobs.Remove(await database.BulkImportJobs.SingleAsync(item => item.Id == jobId));
        await database.SaveChangesAsync();
    }

    private static MultipartFormDataContent Upload(string csv)
    {
        var body = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        body.Add(content, "file", "import.csv"); return body;
    }

    private static async Task Authorize(HttpClient client, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        { email = "admin@premier.com", password = "TenantAdmin123!", deviceId, deviceName = "Bulk import test" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.GetProperty("accessToken").GetString());
    }
}
