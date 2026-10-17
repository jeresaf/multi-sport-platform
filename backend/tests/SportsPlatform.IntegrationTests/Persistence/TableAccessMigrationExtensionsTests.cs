using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SportsPlatform.BuildingBlocks.Persistence;

namespace SportsPlatform.IntegrationTests.Persistence;

/// <summary>The helpers build SQL from names, so only plain snake_case identifiers are accepted (no database needed).</summary>
public sealed class TableAccessMigrationExtensionsTests
{
    [Theory]
    [InlineData("reference_probes\n")]
    [InlineData("Reference_Probes")]
    [InlineData("probes\"; DROP TABLE x; --")]
    [InlineData("1probes")]
    [InlineData("")]
    public void Rejects_names_that_are_not_snake_case_identifiers(string table)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        Should.Throw<ArgumentException>(() => builder.ReadOnlyForRuntimeRoles("conventions_test", table));
        Should.Throw<ArgumentException>(() => builder.AppendOnly("conventions_test", table));
    }

    [Fact]
    public void Rejects_names_longer_than_PostgreSQL_identifiers()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        Should.Throw<ArgumentException>(() => builder.ReadOnlyForRuntimeRoles("conventions_test", new string('a', 64)));
        builder.ReadOnlyForRuntimeRoles("conventions_test", new string('a', 63));
    }

    [Fact]
    public void Quotes_the_names_in_the_generated_sql()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        builder.ReadOnlyForRuntimeRoles("conventions_test", "reference_probes");

        builder.Operations.OfType<SqlOperation>().Single().Sql.ShouldBe(
            "REVOKE INSERT, UPDATE, DELETE ON \"conventions_test\".\"reference_probes\" FROM sp_app, sp_platform;");
    }
}
