using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase9And10Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "interviewer_availability",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interviewer_auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time_utc = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time_utc = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interviewer_availability", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    target_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "offer_letters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_annual_salary = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    body_markdown = table.Column<string>(type: "text", nullable: false),
                    created_by_auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    candidate_response_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer_letters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_memberships", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_interviewer_availability_tenant_subject_day",
                table: "interviewer_availability",
                columns: new[] { "tenant_id", "interviewer_auth_subject", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_recipient_created",
                table: "notifications",
                columns: new[] { "tenant_id", "recipient_auth_subject", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_recipient_read",
                table: "notifications",
                columns: new[] { "tenant_id", "recipient_auth_subject", "read_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_offer_letters_tenant_candidate",
                table: "offer_letters",
                columns: new[] { "tenant_id", "candidate_profile_id" });

            migrationBuilder.CreateIndex(
                name: "ix_offer_letters_tenant_status",
                table: "offer_letters",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_auth_subject_unique",
                table: "tenant_memberships",
                column: "auth_subject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_tenant",
                table: "tenant_memberships",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interviewer_availability");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "offer_letters");

            migrationBuilder.DropTable(
                name: "tenant_memberships");
        }
    }
}
