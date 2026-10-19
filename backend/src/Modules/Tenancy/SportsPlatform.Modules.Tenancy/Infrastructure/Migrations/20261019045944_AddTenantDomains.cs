using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Migrations
{
    /// <summary>
    /// Tenant domains (S02-T3b): global, read-write, no RLS (an RLS coverage exception). Existing tenants get their
    /// platform subdomain from the post-migration step <c>BackfillPlatformSubdomains</c>, which knows the base domain.
    /// </summary>
    public partial class AddTenantDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_domains",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false, collation: "C"),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_domains", x => x.id);
                    table.CheckConstraint("ck_tenant_domains_host", "host ~ '^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$'");
                    table.CheckConstraint("ck_tenant_domains_status", "status IN ('PENDING_VERIFICATION', 'ACTIVE', 'DISABLED')");
                    table.CheckConstraint("ck_tenant_domains_type", "type IN ('PLATFORM_SUBDOMAIN', 'CUSTOM')");
                    table.ForeignKey(
                        name: "fk_tenant_domains_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_domains_host",
                schema: "tenancy",
                table: "tenant_domains",
                column: "host",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_domains_primary",
                schema: "tenancy",
                table: "tenant_domains",
                column: "tenant_id",
                unique: true,
                filter: "is_primary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_domains",
                schema: "tenancy");
        }
    }
}
