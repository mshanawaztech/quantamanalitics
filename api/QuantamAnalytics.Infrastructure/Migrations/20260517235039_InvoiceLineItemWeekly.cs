using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceLineItemWeekly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "days_worked",
                table: "invoice_line_items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "hours_per_day",
                table: "invoice_line_items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "invoice_line_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "week_end_utc",
                table: "invoice_line_items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "week_start_utc",
                table: "invoice_line_items",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_line_items_invoice_id_week_start_utc",
                table: "invoice_line_items",
                columns: new[] { "invoice_id", "week_start_utc" });
            migrationBuilder.Sql(@"
                UPDATE invoice_line_items
                SET
                    days_worked   = COALESCE(days_worked, 1),
                    hours_per_day = COALESCE(hours_per_day, hours);
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_invoice_line_items_invoice_id_week_start_utc",
                table: "invoice_line_items");

            migrationBuilder.DropColumn(
                name: "days_worked",
                table: "invoice_line_items");

            migrationBuilder.DropColumn(
                name: "hours_per_day",
                table: "invoice_line_items");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "invoice_line_items");

            migrationBuilder.DropColumn(
                name: "week_end_utc",
                table: "invoice_line_items");

            migrationBuilder.DropColumn(
                name: "week_start_utc",
                table: "invoice_line_items");
        }
    }
}
