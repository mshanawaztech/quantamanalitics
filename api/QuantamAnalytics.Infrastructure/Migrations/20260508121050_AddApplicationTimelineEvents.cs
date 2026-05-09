using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationTimelineEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_timeline_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    audience = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actor_label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_timeline_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_timeline_events_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_timeline_events_candidate_profiles_candidate_pr",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_timeline_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contractor_auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    contractor_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    period_start_utc = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end_utc = table.Column<DateOnly>(type: "date", nullable: false),
                    hours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewed_by_auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reviewer_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoices_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_timeline_events_application_id",
                table: "application_timeline_events",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_timeline_events_candidate_profile_id",
                table: "application_timeline_events",
                column: "candidate_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_timeline_events_tenant_id_application_id_occurr",
                table: "application_timeline_events",
                columns: new[] { "tenant_id", "application_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_application_timeline_events_tenant_id_candidate_profile_id_",
                table: "application_timeline_events",
                columns: new[] { "tenant_id", "candidate_profile_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_tenant_id_contractor_auth_subject_period_start_utc",
                table: "invoices",
                columns: new[] { "tenant_id", "contractor_auth_subject", "period_start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_tenant_id_status",
                table: "invoices",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_timeline_events");

            migrationBuilder.DropTable(
                name: "invoices");
        }
    }
}
