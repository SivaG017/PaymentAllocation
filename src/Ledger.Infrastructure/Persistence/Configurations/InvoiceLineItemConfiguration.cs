using Ledger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("invoice_line_items");

        builder.HasKey(lineItem => lineItem.Id);

        builder.Property(lineItem => lineItem.Id).HasColumnName("id");
        builder.Property(lineItem => lineItem.InvoiceId).HasColumnName("invoice_id");
        builder.Property(lineItem => lineItem.Description)
            .HasColumnName("description")
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(lineItem => lineItem.OriginalAmount)
            .HasColumnName("original_amount")
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(lineItem => lineItem.DueDate).HasColumnName("due_date").IsRequired();
        builder.Property(lineItem => lineItem.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_invoice_line_items_original_amount_positive", "original_amount > 0");
            table.HasCheckConstraint("ck_invoice_line_items_original_amount_scale", "round(original_amount, 2) = original_amount");
        });

        builder.HasIndex(lineItem => new { lineItem.InvoiceId, lineItem.DueDate, lineItem.CreatedAt, lineItem.Id })
            .HasDatabaseName("ix_invoice_line_items_invoice_oldest_first");
    }
}
