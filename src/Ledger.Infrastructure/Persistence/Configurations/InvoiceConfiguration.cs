using Ledger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id).HasColumnName("id");
        builder.Property(invoice => invoice.CustomerReference)
            .HasColumnName("customer_reference")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(invoice => invoice.CreatedAt).HasColumnName("created_at").IsRequired();

        builder
            .HasMany(invoice => invoice.LineItems)
            .WithOne(lineItem => lineItem.Invoice)
            .HasForeignKey(lineItem => lineItem.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(invoice => invoice.LineItems).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
