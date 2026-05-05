using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auth_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    full_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    headline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    resume_object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    resume_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    resume_uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_profiles_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_profiles_tenant_id_auth_subject",
                table: "candidate_profiles",
                columns: new[] { "tenant_id", "auth_subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidate_profiles_tenant_id_email",
                table: "candidate_profiles",
                columns: new[] { "tenant_id", "email" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_profiles");
        }
    }
}
