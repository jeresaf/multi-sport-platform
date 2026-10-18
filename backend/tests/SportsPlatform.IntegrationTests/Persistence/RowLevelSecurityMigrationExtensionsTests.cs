using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SportsPlatform.BuildingBlocks.Persistence;

namespace SportsPlatform.IntegrationTests.Persistence;

/// <summary><c>EnableTenantRls</c> builds SQL from names, so only snake_case identifiers pass (no database needed).</summary>
public sealed class RowLevelSecurityMigrationExtensionsTests
{
    [Theory]
    [InlineData("tenant_probes\n")]
    [InlineData("Tenant_Probes")]
    [InlineData("probes\"; DROP TABLE x; --")]
    [InlineData("")]
    public void Rejects_names_that_are_not_snake_case_identifiers(string table)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        Should.Throw<ArgumentException>(() => builder.EnableTenantRls("conventions_test", table));
        Should.Throw<ArgumentException>(() => builder.DisableTenantRls("conventions_test", table));
    }

    [Fact]
    public void Enables_forces_and_adds_the_policy_for_reads_and_writes()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        builder.EnableTenantRls("conventions_test", "tenant_probes");

        const string condition = "tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid";
        builder.Operations.OfType<SqlOperation>().Select(o => o.Sql).ShouldBe(
        [
            "ALTER TABLE \"conventions_test\".\"tenant_probes\" ENABLE ROW LEVEL SECURITY;",
            "ALTER TABLE \"conventions_test\".\"tenant_probes\" FORCE ROW LEVEL SECURITY;",
            $"CREATE POLICY tenant_isolation ON \"conventions_test\".\"tenant_probes\" USING ({condition}) WITH CHECK ({condition});",
        ]);
    }
}
