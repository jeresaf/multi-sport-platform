using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// Public GETs sent straight into a <see cref="TestServer"/>, so a test chooses the request's host and the address it
/// comes from (an <see cref="HttpClient"/> leaves the remote address unset).
/// </summary>
internal static class PublicRequests
{
    public const string SitePath = "/api/public/v1/site";

    /// <summary>The address <see cref="PublicTestApp"/> trusts as the public web's server (D7).</summary>
    public static readonly IPAddress Proxy = IPAddress.Parse("10.20.0.5");

    /// <summary>Any other caller.</summary>
    public static readonly IPAddress Browser = IPAddress.Parse("203.0.113.7");

    public static async Task<PublicResponse> GetAsync(
        this TestServer server, string pathAndQuery, string host, IPAddress? from = null, params (string Name, string Value)[] headers)
    {
        ArgumentNullException.ThrowIfNull(server);
        var query = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        var context = await server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = query < 0 ? pathAndQuery : pathAndQuery[..query];
            c.Request.QueryString = query < 0 ? QueryString.Empty : new QueryString(pathAndQuery[query..]);
            c.Request.Host = new HostString(host);
            c.Connection.RemoteIpAddress = from ?? Browser;
            foreach (var (name, value) in headers)
            {
                c.Request.Headers.Append(name, value);
            }
        }, TestContext.Current.CancellationToken);

        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        return new PublicResponse(context.Response.StatusCode, context.Response.Headers, body);
    }
}

internal sealed record PublicResponse(int Status, IHeaderDictionary Headers, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement.Clone();

    public void ShouldBeSite(string slug)
    {
        Status.ShouldBe(StatusCodes.Status200OK, Body);
        Json.GetProperty("slug").GetString().ShouldBe(slug);
    }

    public void ShouldBeProblem(int status, string code)
    {
        Status.ShouldBe(status, Body);
        Headers.ContentType.ToString().ShouldStartWith("application/problem+json");
        Json.GetProperty("code").GetString().ShouldBe(code);
    }
}
