using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class SecurityControlTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public SecurityControlTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task DirectApi_AddsSecurityHeadersAndRestrictsCors()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("frame-ancestors 'none'");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("no-referrer");

        using var rejectedPreflight = new HttpRequestMessage(HttpMethod.Options, "/health");
        rejectedPreflight.Headers.Add("Origin", "https://attacker.invalid");
        rejectedPreflight.Headers.Add("Access-Control-Request-Method", "GET");
        var rejected = await client.SendAsync(rejectedPreflight);
        rejected.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();

        using var acceptedPreflight = new HttpRequestMessage(HttpMethod.Options, "/health");
        acceptedPreflight.Headers.Add("Origin", "http://localhost:3000");
        acceptedPreflight.Headers.Add("Access-Control-Request-Method", "GET");
        var accepted = await client.SendAsync(acceptedPreflight);
        accepted.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle("http://localhost:3000");
    }

    [Fact]
    public async Task Observability_PropagatesCorrelationAndReportsComponentHealth()
    {
        using var client = _factory.CreateClient();
        const string correlationId = "mobile-score-sync-42";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var live = await client.SendAsync(request);
        live.StatusCode.Should().Be(HttpStatusCode.OK);
        live.Headers.GetValues("X-Correlation-Id").Should().ContainSingle(correlationId);
        live.Headers.GetValues("X-Request-Id").Should().ContainSingle(value => !string.IsNullOrWhiteSpace(value));
        using var liveJson = JsonDocument.Parse(await live.Content.ReadAsStringAsync());
        liveJson.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        liveJson.RootElement.GetProperty("checks").EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .Should().ContainSingle("self");

        var ready = await client.GetAsync("/health/ready");
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        using var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync());
        var checks = readyJson.RootElement.GetProperty("checks").EnumerateArray()
            .ToDictionary(
                check => check.GetProperty("name").GetString()!,
                check => check.GetProperty("status").GetString());
        checks.Should().ContainKeys("postgresql", "redis");
        checks["postgresql"].Should().Be("Healthy");
        checks["redis"].Should().BeOneOf("Healthy", "Degraded");

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        invalidRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", "invalid correlation value");
        var invalid = await client.SendAsync(invalidRequest);
        invalid.Headers.GetValues("X-Correlation-Id").Should().ContainSingle(value =>
            value != "invalid correlation value" && value.Length == 32);
    }

    [Fact]
    public async Task Auth_ValidatesInputLocksAccountsAndRotatesRefreshTokens()
    {
        using var client = _factory.CreateClient();
        var invalid = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email", password = "short", firstName = "", lastName = ""
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var suffix = Guid.NewGuid().ToString("N");
        var email = $"security-{suffix}@example.com";
        var password = "Security123!";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password, firstName = "Security", lastName = "Test"
        });
        registration.StatusCode.Should().Be(HttpStatusCode.OK,
            await registration.Content.ReadAsStringAsync());
        using var registeredJson = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var oldRefreshToken = registeredJson.RootElement.GetProperty("refreshToken").GetString()!;

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = oldRefreshToken
        });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = oldRefreshToken
        });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email, password = "WrongPassword123!"
            });
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        var locked = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await locked.Content.ReadAsStringAsync()).Should().Contain("temporarily locked");
    }

    [Fact]
    public async Task HighRiskEndpoints_AreRateLimitedAndCsvContentIsValidated()
    {
        using var client = _factory.CreateClient();
        HttpResponseMessage? limited = null;
        for (var request = 0; request < 11; request++)
            limited = await client.PostAsJsonAsync("/api/auth/forgot-password", new
            {
                email = $"absent-{request}@example.com"
            });
        limited!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@premier.com", password = "TenantAdmin123!"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginJson.RootElement.GetProperty("accessToken").GetString());

        using var form = new MultipartFormDataContent();
        var binary = new ByteArrayContent(Encoding.UTF8.GetBytes("Name\0Malicious"));
        binary.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(binary, "file", "participants.csv");
        var upload = await client.PostAsync("/api/bulkimport/athletes", form);
        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            await upload.Content.ReadAsStringAsync());
        (await upload.Content.ReadAsStringAsync()).Should().Contain("Binary or null-byte");
    }

    [Fact]
    public async Task Mfa_EnrollmentIsVerifiedAndRequiredAtLogin()
    {
        using var client = _factory.CreateClient();
        var email = $"mfa-{Guid.NewGuid():N}@example.com";
        const string password = "Security123!";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password, firstName = "Mfa", lastName = "Test"
        });
        registration.StatusCode.Should().Be(HttpStatusCode.OK);
        using var registrationJson = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", registrationJson.RootElement.GetProperty("accessToken").GetString());

        var enrollment = await client.PostAsync("/api/auth/mfa/enroll", null);
        enrollment.StatusCode.Should().Be(HttpStatusCode.OK);
        using var enrollmentJson = JsonDocument.Parse(await enrollment.Content.ReadAsStringAsync());
        var secret = enrollmentJson.RootElement.GetProperty("secret").GetString()!;
        var code = GenerateTotp(secret);
        var verification = await client.PostAsJsonAsync("/api/auth/mfa/verify", new { code });
        verification.StatusCode.Should().Be(HttpStatusCode.OK,
            await verification.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Authorization = null;
        var passwordOnly = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        passwordOnly.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var withMfa = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, mfaCode = GenerateTotp(secret)
        });
        withMfa.StatusCode.Should().Be(HttpStatusCode.OK,
            await withMfa.Content.ReadAsStringAsync());
    }

    private static string GenerateTotp(string secret)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in secret)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits < 8) continue;
            bytes.Add((byte)(buffer >> (bits - 8)));
            bits -= 8;
        }
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(
            counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }
}
