using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingChecklistBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "onboarding_checklist_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    item_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    candidate_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reviewer_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_onboarding_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_onboarding_checklist_items_candidate_profiles_candidate_pro",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_onboarding_checklist_items_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_checklist_items_candidate_profile_id",
                table: "onboarding_checklist_items",
                column: "candidate_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_checklist_items_tenant_id_candidate_profile_id_a",
                table: "onboarding_checklist_items",
                columns: new[] { "tenant_id", "candidate_profile_id", "assigned_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_checklist_items_tenant_id_status",
                table: "onboarding_checklist_items",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_checklist_items");
        }
    }
}
