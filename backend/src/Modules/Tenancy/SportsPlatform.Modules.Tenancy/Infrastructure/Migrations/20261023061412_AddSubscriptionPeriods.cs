using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Migrations
{
    /// <summary>S04-T2a: tenants' subscription periods, tenant-owned (RLS). No tenant has a period yet.</summary>
    public partial class AddSubscriptionPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_periods",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expiring_window_days = table.Column<int>(type: "integer", nullable: false),
                    grace_days = table.Column<int>(type: "integer", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_periods", x => x.id);
                    table.CheckConstraint("ck_subscription_periods_cancelled", "(cancelled_at IS NULL AND cancelled_by IS NULL AND cancel_reason IS NULL) OR (cancelled_at IS NOT NULL AND cancelled_by IS NOT NULL AND cancel_reason IS NOT NULL)");
                    table.CheckConstraint("ck_subscription_periods_dates", "ends_on >= starts_on");
                    table.CheckConstraint("ck_subscription_periods_expiring_window_days", "expiring_window_days BETWEEN 0 AND 365");
                    table.CheckConstraint("ck_subscription_periods_grace_days", "grace_days BETWEEN 0 AND 365");
                    table.CheckConstraint("ck_subscription_periods_kind", "kind IN ('PAID', 'TRIAL', 'COMPLIMENTARY')");
                    table.ForeignKey(
                        name: "fk_subscription_periods_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_periods_tenant_id_starts_on",
                schema: "tenancy",
                table: "subscription_periods",
                columns: new[] { "tenant_id", "starts_on" });

            migrationBuilder.EnableTenantRls("tenancy", "subscription_periods");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("tenancy", "subscription_periods");

            migrationBuilder.DropTable(
                name: "subscription_periods",
                schema: "tenancy");
        }
    }
}
