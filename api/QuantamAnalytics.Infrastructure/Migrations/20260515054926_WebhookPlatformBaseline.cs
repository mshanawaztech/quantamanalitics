using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WebhookPlatformBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_attempted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_response_status_code = table.Column<int>(type: "integer", nullable: true),
                    last_response_body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_deliveries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    event_types = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    secret_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    secret_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_auth_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_delivery_attempt_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_successful_delivery_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consecutive_failure_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_subscriptions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_status_next_attempt",
                table: "webhook_deliveries",
                columns: new[] { "status", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_tenant_created",
                table: "webhook_deliveries",
                columns: new[] { "tenant_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_subscriptions_tenant_active",
                table: "webhook_subscriptions",
                columns: new[] { "tenant_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions");
        }
    }
}
