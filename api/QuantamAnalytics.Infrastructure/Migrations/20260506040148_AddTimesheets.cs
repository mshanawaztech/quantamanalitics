using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimesheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "timesheets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contractor_auth_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contractor_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    week_start_utc = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reviewed_by_auth_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_timesheets", x => x.id);
                    table.ForeignKey(
                        name: "fk_timesheets_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "time_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timesheet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    entry_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_entries_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_time_entries_timesheets_timesheet_id",
                        column: x => x.timesheet_id,
                        principalTable: "timesheets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_tenant_id_timesheet_id_work_date_entry_type",
                table: "time_entries",
                columns: new[] { "tenant_id", "timesheet_id", "work_date", "entry_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_tenant_id_work_date",
                table: "time_entries",
                columns: new[] { "tenant_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_timesheet_id",
                table: "time_entries",
                column: "timesheet_id");

            migrationBuilder.CreateIndex(
                name: "ix_timesheets_tenant_id_contractor_auth_subject_week_start_utc",
                table: "timesheets",
                columns: new[] { "tenant_id", "contractor_auth_subject", "week_start_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_timesheets_tenant_id_status",
                table: "timesheets",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "time_entries");

            migrationBuilder.DropTable(
                name: "timesheets");
        }
    }
}
