using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JobSoftDeleteBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at_utc",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_auth_subject",
                table: "jobs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_tenant_deleted",
                table: "jobs",
                columns: new[] { "tenant_id", "deleted_at_utc" },
                filter: "is_deleted = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_jobs_tenant_deleted",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "deleted_by_auth_subject",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "jobs");
        }
    }
}
