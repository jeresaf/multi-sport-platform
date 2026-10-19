using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace SportsPlatform.Host.Tenancy;

/// <summary>
/// <c>X-Forwarded-Host</c> and <c>X-Forwarded-For</c> from trusted proxies only (S02 D7): the public web's server calls
/// the API with the browser's host in the first; the second gives the client's address to the sign-in rate limits
/// (S03 D15) and the logs. Nothing is trusted unless listed in <c>ForwardedHeaders:KnownProxies</c> (IP addresses) or
/// <c>ForwardedHeaders:KnownNetworks</c> (CIDR); the framework's loopback defaults are removed, so a forwarded host
/// from anyone else is ignored and the request's own host counts. With neither configured, forwarding is off.
/// </summary>
public static class ForwardedHost
{
    public const string SectionName = "ForwardedHeaders";

    public static IServiceCollection AddForwardedHost(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        // Parsed now, so a bad value stops startup rather than failing requests.
        var proxies = (section.GetSection("KnownProxies").Get<string[]>() ?? []).Select(IPAddress.Parse).ToList();
        var networks = (section.GetSection("KnownNetworks").Get<string[]>() ?? []).Select(n => System.Net.IPNetwork.Parse(n)).ToList();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // With both lists empty the middleware trusts every caller, so no proxy configured means no forwarding.
            options.ForwardedHeaders = proxies.Count + networks.Count > 0 ? ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedFor : ForwardedHeaders.None;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
#pragma warning disable ASPDEPR005 // Obsolete, but still holds the 127.0.0.0/8 default, which the middleware still trusts.
            options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
        return services;
    }

    /// <summary>First in the pipeline, so logging, tenant resolution and rate limits see the forwarded host and address.</summary>
    public static IApplicationBuilder UseForwardedHost(this IApplicationBuilder app) => app.UseForwardedHeaders();
}
