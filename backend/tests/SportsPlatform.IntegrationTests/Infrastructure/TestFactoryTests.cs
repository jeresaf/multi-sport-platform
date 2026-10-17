using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SportsPlatform.IntegrationTests.Infrastructure;

public sealed class TestFactoryTests
{
    // The plain factory runs in Development and would migrate the developer's database (see HostOnlyApp).
    [Fact]
    public void Tests_use_HostOnlyApp_or_TestApp_never_the_plain_WebApplicationFactory()
    {
        var plain = typeof(WebApplicationFactory<Program>);

        var offenders = typeof(TestFactoryTests).Assembly.GetTypes()
            .Where(type => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericArguments().Contains(plain))
                || type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .Any(ctor => ctor.GetParameters().Any(p => p.ParameterType == plain)))
            .Select(type => type.FullName)
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void HostOnlyApp_runs_in_the_Testing_environment()
    {
        using var app = new HostOnlyApp();

        app.Services.GetRequiredService<IHostEnvironment>().EnvironmentName
            .ShouldBe(TestEnvironment.Name);
    }
}
