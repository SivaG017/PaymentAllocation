using Ledger.Domain.Entities;
using Ledger.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Persistence.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.InvoiceId).HasColumnName("invoice_id");
        builder.Property(entry => entry.InvoiceLineItemId).HasColumnName("invoice_line_item_id");
        builder.Property(entry => entry.EntryType)
            .HasColumnName("entry_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(entry => entry.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entry => entry.PaymentId).HasColumnName("payment_id");

        builder
            .HasOne(entry => entry.Invoice)
            .WithMany()
            .HasForeignKey(entry => entry.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(entry => entry.InvoiceLineItem)
            .WithMany()
            .HasForeignKey(entry => entry.InvoiceLineItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_ledger_entries_amount_non_zero", "amount <> 0");
            table.HasCheckConstraint("ck_ledger_entries_amount_scale", "round(amount, 2) = amount");
            table.HasCheckConstraint("ck_ledger_entries_charge_positive", $"entry_type <> '{LedgerEntryType.Charge}' OR amount > 0");
            table.HasCheckConstraint("ck_ledger_entries_payments_negative", $"entry_type = '{LedgerEntryType.Charge}' OR amount < 0");
            table.HasCheckConstraint("ck_ledger_entries_credit_has_no_line", $"entry_type <> '{LedgerEntryType.CustomerCredit}' OR invoice_line_item_id IS NULL");
            table.HasCheckConstraint("ck_ledger_entries_line_entries_have_line", $"entry_type = '{LedgerEntryType.CustomerCredit}' OR invoice_line_item_id IS NOT NULL");
        });

        builder.HasIndex(entry => entry.InvoiceId).HasDatabaseName("ix_ledger_entries_invoice_id");
        builder.HasIndex(entry => entry.InvoiceLineItemId).HasDatabaseName("ix_ledger_entries_invoice_line_item_id");
        builder.HasIndex(entry => entry.PaymentId).HasDatabaseName("ix_ledger_entries_payment_id");
    }
}
