using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Modules.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicImagePurposes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_purpose",
                schema: "platform",
                table: "media_assets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_public_purpose",
                schema: "platform",
                table: "media_assets",
                sql: "(visibility = 'PUBLIC') = (purpose IN ('ORG_LOGO', 'TEAM_LOGO', 'TEAM_BADGE'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_purpose",
                schema: "platform",
                table: "media_assets",
                sql: "purpose IN ('PERSON_PHOTO', 'CONSENT_EVIDENCE', 'ORG_LOGO', 'TEAM_LOGO', 'TEAM_BADGE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_public_purpose",
                schema: "platform",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_purpose",
                schema: "platform",
                table: "media_assets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_purpose",
                schema: "platform",
                table: "media_assets",
                sql: "purpose IN ('PERSON_PHOTO', 'CONSENT_EVIDENCE')");
        }
    }
}
