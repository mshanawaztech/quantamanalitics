using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CheckrBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "background_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    candidate_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requested_by_auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    package_slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_report_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status_detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_background_checks", x => x.id);
                    table.ForeignKey(
                        name: "fk_background_checks_candidate_profiles_candidate_profile_id",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_background_checks_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_background_checks_candidate_profile_id",
                table: "background_checks",
                column: "candidate_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_background_checks_provider_report_id",
                table: "background_checks",
                column: "provider_report_id",
                unique: true,
                filter: "provider_report_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_background_checks_tenant_id_status_requested_at_utc",
                table: "background_checks",
                columns: new[] { "tenant_id", "status", "requested_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_checks");
        }
    }
}
