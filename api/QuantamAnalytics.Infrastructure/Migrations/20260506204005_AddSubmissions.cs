using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    candidate_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    client_company_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    pitch_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    submitted_by_auth_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    client_decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    submitted_to_client_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    client_decision_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_submissions_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_submissions_candidate_profiles_candidate_profile_id",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_submissions_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_submissions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_application_id",
                table: "submissions",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_candidate_profile_id",
                table: "submissions",
                column: "candidate_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_job_id",
                table: "submissions",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_tenant_id_application_id",
                table: "submissions",
                columns: new[] { "tenant_id", "application_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_tenant_id_status",
                table: "submissions",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submissions");
        }
    }
}
