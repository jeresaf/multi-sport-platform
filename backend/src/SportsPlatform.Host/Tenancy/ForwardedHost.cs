using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace SportsPlatform.Host.Tenancy;

/// <summary>
/// <c>X-Forwarded-Host</c> from trusted proxies only (S02 D7): the public web's server calls the API with the browser's
/// host in that header. Nothing is trusted unless listed in <c>ForwardedHeaders:KnownProxies</c> (IP addresses) or
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
        var proxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        var networks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // With both lists empty the middleware trusts every caller, so no proxy configured means no forwarding.
            options.ForwardedHeaders = proxies.Length + networks.Length > 0 ? ForwardedHeaders.XForwardedHost : ForwardedHeaders.None;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
#pragma warning disable ASPDEPR005 // Obsolete, but still holds the 127.0.0.0/8 default, which the middleware still trusts.
            options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
        return services;
    }

    /// <summary>First in the pipeline, so logging and tenant resolution see the forwarded host.</summary>
    public static IApplicationBuilder UseForwardedHost(this IApplicationBuilder app) => app.UseForwardedHeaders();
}
