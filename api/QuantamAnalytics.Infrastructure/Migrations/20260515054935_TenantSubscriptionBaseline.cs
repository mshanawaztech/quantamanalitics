using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantSubscriptionBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    included_seats = table.Column<int>(type: "integer", nullable: false),
                    price_per_seat = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    trial_ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_period_ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    stripe_customer_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    stripe_subscription_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_subscriptions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_subscriptions_stripe_sub",
                table: "tenant_subscriptions",
                column: "stripe_subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_subscriptions_tenant_unique",
                table: "tenant_subscriptions",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_subscriptions");
        }
    }
}
