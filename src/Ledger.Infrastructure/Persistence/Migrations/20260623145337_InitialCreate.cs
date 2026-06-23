using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_line_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_line_items", x => x.id);
                    table.CheckConstraint("ck_invoice_line_items_original_amount_positive", "original_amount > 0");
                    table.CheckConstraint("ck_invoice_line_items_original_amount_scale", "round(original_amount, 2) = original_amount");
                    table.ForeignKey(
                        name: "FK_invoice_line_items_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_line_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entry_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_ledger_entries_amount_non_zero", "amount <> 0");
                    table.CheckConstraint("ck_ledger_entries_amount_scale", "round(amount, 2) = amount");
                    table.CheckConstraint("ck_ledger_entries_charge_positive", "entry_type <> 'Charge' OR amount > 0");
                    table.CheckConstraint("ck_ledger_entries_credit_has_no_line", "entry_type <> 'CustomerCredit' OR invoice_line_item_id IS NULL");
                    table.CheckConstraint("ck_ledger_entries_line_entries_have_line", "entry_type = 'CustomerCredit' OR invoice_line_item_id IS NOT NULL");
                    table.CheckConstraint("ck_ledger_entries_payments_negative", "entry_type = 'Charge' OR amount < 0");
                    table.ForeignKey(
                        name: "FK_ledger_entries_invoice_line_items_invoice_line_item_id",
                        column: x => x.invoice_line_item_id,
                        principalTable: "invoice_line_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_line_items_invoice_oldest_first",
                table: "invoice_line_items",
                columns: new[] { "invoice_id", "due_date", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_invoice_id",
                table: "ledger_entries",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_invoice_line_item_id",
                table: "ledger_entries",
                column: "invoice_line_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_payment_id",
                table: "ledger_entries",
                column: "payment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "invoice_line_items");

            migrationBuilder.DropTable(
                name: "invoices");
        }
    }
}
