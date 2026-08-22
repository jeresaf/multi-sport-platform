using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicantRegistrationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicantUserId",
                table: "Registrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FormSubmissionId",
                table: "Registrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicantUserId",
                table: "RegistrationFormSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegistrationId",
                table: "RegistrationFormSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicantUserId",
                table: "Participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_ApplicantUserId",
                table: "Registrations",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_FormSubmissionId",
                table: "Registrations",
                column: "FormSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationFormSubmissions_ApplicantUserId",
                table: "RegistrationFormSubmissions",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationFormSubmissions_RegistrationId",
                table: "RegistrationFormSubmissions",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_TenantId_ApplicantUserId",
                table: "Participants",
                columns: new[] { "TenantId", "ApplicantUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Registrations_ApplicantUserId",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_FormSubmissionId",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "IX_RegistrationFormSubmissions_ApplicantUserId",
                table: "RegistrationFormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_RegistrationFormSubmissions_RegistrationId",
                table: "RegistrationFormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_Participants_TenantId_ApplicantUserId",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "ApplicantUserId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "FormSubmissionId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "ApplicantUserId",
                table: "RegistrationFormSubmissions");

            migrationBuilder.DropColumn(
                name: "RegistrationId",
                table: "RegistrationFormSubmissions");

            migrationBuilder.DropColumn(
                name: "ApplicantUserId",
                table: "Participants");
        }
    }
}
