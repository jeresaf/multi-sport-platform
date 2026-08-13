using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompetitionPresentationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PresentationConfiguration",
                table: "Competitions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresentationConfiguration",
                table: "Competitions");
        }
    }
}
