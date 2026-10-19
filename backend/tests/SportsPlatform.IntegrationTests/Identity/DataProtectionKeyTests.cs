using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// S03 D14: the data protection key ring lives in <c>identity.data_protection_keys</c>, so what one API instance
/// protects (cookies, Identity's tokens) another can read, and restarts keep it.
/// </summary>
public sealed class DataProtectionKeyTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task A_second_instance_reads_what_the_first_protected_with_keys_from_the_database()
    {
        // Two fresh instances: the shared app caches a key ring whose rows the reset removed.
        await using var first = new TestApp(Fixture.AppSettings);
        await using var second = new TestApp(Fixture.AppSettings);

        var protectedText = Protector(first).Protect("a session cookie payload");

        (await QueryAsync(DatabaseRoles.Migrator, "SELECT count(*) FROM identity.data_protection_keys", r => r.GetInt64(0)))
            .ShouldHaveSingleItem().ShouldBe(1);
        Protector(second).Unprotect(protectedText).ShouldBe("a session cookie payload");
    }

    private static IDataProtector Protector(TestApp app) =>
        app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("SportsPlatform.IntegrationTests");
}
