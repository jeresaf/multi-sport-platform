using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Host.Authorization;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// Sign-in and forgot-password rate limits (S03 D15): per client IP before the audit (a flood writes no audit rows), per
/// normalised email after the request is read (audited as DENIED); <c>429 AUTH_RATE_LIMITED</c> with <c>Retry-After</c>.
/// Each test runs its own app with small limits: the limiters live in memory, and the shared app's are set out of reach.
/// </summary>
public sealed class RateLimitTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly IPAddress _kampala = IPAddress.Parse("203.0.113.7");
    private static readonly IPAddress _entebbe = IPAddress.Parse("198.51.100.23");

    // S03 acceptance: the rate limit answers 429.
    [Fact]
    public async Task Sign_in_is_limited_per_client_address_before_the_audit()
    {
        await using var app = AppWithLimits(("LoginPerIp", 3));
        string[] emails = ["grace.nakato@kmsa.example", "daniel.kato@kmsa.example", "brian.ssemakula@kmsa.example", "esther.nabirye@kmsa.example"];

        var responses = new List<AccountResponse>();
        foreach (var email in emails)
        {
            responses.Add(await LoginAsync(app.Server, email, _kampala));
        }

        var elsewhere = await LoginAsync(app.Server, "grace.nakato@kmsa.example", _entebbe);

        responses.Take(3).ShouldAllBe(r => r.Status == StatusCodes.Status401Unauthorized, string.Join(" ", responses.Select(r => r.Body)));
        responses[3].ShouldBeRateLimited();
        elsewhere.ShouldHaveStatus(StatusCodes.Status401Unauthorized);
        await AccountTestData.DeliverOutboxAsync(app.Services, Ct);
        (await AuditTestData.EntriesAsync(app.Services, Ct)).Count.ShouldBe(4); // three from Kampala, one from Entebbe
    }

    [Fact]
    public async Task Sign_in_is_limited_per_email_in_any_case_and_audited_as_denied()
    {
        await using var app = AppWithLimits(("LoginPerEmail", 2));
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(app.Services, "grace.nakato@kmsa.example", password, Ct);

        (await LoginAsync(app.Server, "grace.nakato@kmsa.example", _kampala, TestPasswords.New())).ShouldHaveStatus(401);
        (await LoginAsync(app.Server, " Grace.Nakato@KMSA.example", _entebbe, TestPasswords.New())).ShouldHaveStatus(401);
        var known = await LoginAsync(app.Server, "GRACE.NAKATO@KMSA.EXAMPLE", _kampala, password);
        (await LoginAsync(app.Server, "brian.ssemakula@kmsa.example", _kampala)).ShouldHaveStatus(401);
        (await LoginAsync(app.Server, "brian.ssemakula@kmsa.example", _kampala)).ShouldHaveStatus(401);
        var unknown = await LoginAsync(app.Server, "brian.ssemakula@kmsa.example", _kampala);

        known.ShouldBeRateLimited();
        unknown.ShouldBeRateLimited();
        known.WithoutCorrelationId().ShouldBe(unknown.WithoutCorrelationId()); // a known email answers like an unknown one
        await AccountTestData.DeliverOutboxAsync(app.Services, Ct);
        var refused = (await AuditTestData.EntriesAsync(app.Services, Ct)).Where(e => e.ErrorCode == "AUTH_RATE_LIMITED").ToList();
        refused.Count.ShouldBe(2);
        refused.ShouldAllBe(e => e.ActionCode == "account.login" && e.Outcome == AuditOutcome.Denied && e.EntityId == null);
    }

    [Fact]
    public async Task Forgot_password_is_limited_per_client_address_and_per_email()
    {
        await using var app = AppWithLimits(("ForgotPasswordPerIp", 2), ("ForgotPasswordPerEmail", 1));

        var first = await ForgotAsync(app.Server, "grace.nakato@kmsa.example", _kampala);
        var sameEmail = await ForgotAsync(app.Server, "grace.nakato@kmsa.example", _entebbe);
        var second = await ForgotAsync(app.Server, "daniel.kato@kmsa.example", _kampala);
        var thirdFromKampala = await ForgotAsync(app.Server, "brian.ssemakula@kmsa.example", _kampala);

        first.ShouldHaveStatus(StatusCodes.Status202Accepted);
        sameEmail.ShouldBeRateLimited();
        second.ShouldHaveStatus(StatusCodes.Status202Accepted);
        thirdFromKampala.ShouldBeRateLimited();
    }

    // S02 D7 (owner, T2b): behind a trusted proxy the limit counts the client's address, not the proxy's.
    [Fact]
    public async Task Behind_a_trusted_proxy_each_client_has_its_own_limit()
    {
        // TestApp's settings come too late for the forwarded headers, which are read when services are added.
        await using var app = await PublicTestApp.StartAsync(
            Fixture, TestEnvironment.Name, Ct, ("Identity:RateLimits:LoginPerIp:PermitLimit", "1"), ("Identity:RateLimits:LoginPerIp:WindowSeconds", "3600"));

        var kampala = await LoginAsync(app.Server, "grace.nakato@kmsa.example", PublicRequests.Proxy, forwardedFor: _kampala);
        var entebbe = await LoginAsync(app.Server, "daniel.kato@kmsa.example", PublicRequests.Proxy, forwardedFor: _entebbe);
        var kampalaAgain = await LoginAsync(app.Server, "brian.ssemakula@kmsa.example", PublicRequests.Proxy, forwardedFor: _kampala);
        // An untrusted caller's X-Forwarded-For is ignored: its own address counts.
        var spoofed = await LoginAsync(app.Server, "esther.nabirye@kmsa.example", _entebbe, forwardedFor: IPAddress.Parse("192.0.2.44"));

        kampala.ShouldHaveStatus(StatusCodes.Status401Unauthorized);
        entebbe.ShouldHaveStatus(StatusCodes.Status401Unauthorized);
        kampalaAgain.ShouldBeRateLimited();
        spoofed.ShouldBeRateLimited();
    }

    private TestApp AppWithLimits(params (string Limit, int Permits)[] limits)
    {
        var settings = new Dictionary<string, string?>(Fixture.AppSettings);
        foreach (var (limit, permits) in limits)
        {
            settings[$"Identity:RateLimits:{limit}:PermitLimit"] = permits.ToString(System.Globalization.CultureInfo.InvariantCulture);
            settings[$"Identity:RateLimits:{limit}:WindowSeconds"] = "3600";
        }

        return new TestApp(settings);
    }

    private static Task<AccountResponse> LoginAsync(
        TestServer server, string email, IPAddress from, string? password = null, IPAddress? forwardedFor = null) =>
        PostAsync(server, AccountTestData.LoginPath, new { email, password = password ?? TestPasswords.New() }, from, forwardedFor);

    private static Task<AccountResponse> ForgotAsync(TestServer server, string email, IPAddress from) =>
        PostAsync(server, AccountTestData.ForgotPasswordPath, new { email }, from, forwardedFor: null);

    /// <summary>Sent straight into the server, so the test chooses the remote address (an HttpClient leaves it unset).</summary>
    private static async Task<AccountResponse> PostAsync(TestServer server, Uri path, object body, IPAddress from, IPAddress? forwardedFor)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(body, JsonSerializerOptions.Web);
        var context = await server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Post;
            c.Request.Scheme = "https";
            c.Request.Host = new HostString("localhost");
            c.Request.Path = path.OriginalString;
            c.Request.ContentType = "application/json";
            c.Request.ContentLength = json.Length;
            c.Request.Body = new MemoryStream(json);
            c.Features.Set<IHttpRequestBodyDetectionFeature>(new HasBody()); // TestServer marks a SendAsync request bodiless
            c.Request.Headers[CsrfProtection.HeaderName] = CsrfProtection.HeaderValue;
            if (forwardedFor is not null)
            {
                c.Request.Headers["X-Forwarded-For"] = forwardedFor.ToString();
            }

            c.Connection.RemoteIpAddress = from;
        }, Ct);

        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return new AccountResponse(context.Response.StatusCode, context.Response.Headers, await reader.ReadToEndAsync(Ct));
    }

    private sealed class HasBody : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed record AccountResponse(int Status, IHeaderDictionary Headers, string Body)
    {
        public JsonElement Json => JsonDocument.Parse(Body).RootElement.Clone();

        public string WithoutCorrelationId() => string.Join("|", Json.EnumerateObject()
            .Where(property => property.Name != "correlationId")
            .Select(property => $"{property.Name}={property.Value}"));

        public void ShouldHaveStatus(int status) => Status.ShouldBe(status, Body);

        public void ShouldBeRateLimited()
        {
            Status.ShouldBe(StatusCodes.Status429TooManyRequests, Body);
            Headers.ContentType.ToString().ShouldStartWith("application/problem+json");
            Headers.CacheControl.ToString().ShouldBe("no-store");
            Json.GetProperty("code").GetString().ShouldBe("AUTH_RATE_LIMITED");
            int.Parse(Headers.RetryAfter.ToString(), System.Globalization.CultureInfo.InvariantCulture).ShouldBeInRange(1, 3600);
        }
    }
}
