using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantamAnalytics.Infrastructure.Migrations
{
    /// <summary>
    /// qa005 — adds per-invoice Remit-to override columns and a vendor name.
    ///
    /// Five nullable columns are added to <c>invoices</c>:
    ///   - <c>remit_bank_name</c>
    ///   - <c>remit_account_number</c>
    ///   - <c>remit_routing_number</c>
    ///   - <c>remit_contact_phone</c>
    ///   - <c>vendor_name</c>
    ///
    /// All NULL on existing rows. NULL on read means "fall back to the
    /// tenant's branding default" — the PDF renderer applies that fallback
    /// chain. No backfill required: existing invoices were rendered with
    /// branding defaults and will continue to be.
    /// </summary>
    public partial class InvoiceRemitOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "remit_bank_name",
                table: "invoices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remit_account_number",
                table: "invoices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remit_routing_number",
                table: "invoices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remit_contact_phone",
                table: "invoices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vendor_name",
                table: "invoices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "vendor_name", table: "invoices");
            migrationBuilder.DropColumn(name: "remit_contact_phone", table: "invoices");
            migrationBuilder.DropColumn(name: "remit_routing_number", table: "invoices");
            migrationBuilder.DropColumn(name: "remit_account_number", table: "invoices");
            migrationBuilder.DropColumn(name: "remit_bank_name", table: "invoices");
        }
    }
}
