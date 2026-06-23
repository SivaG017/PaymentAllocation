namespace Ledger.Domain.Entities;

public sealed class InvoiceLineItem
{
    private InvoiceLineItem()
    {
    }

    public InvoiceLineItem(
        Guid id,
        Guid invoiceId,
        string description,
        decimal originalAmount,
        DateOnly dueDate,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Line item id must be provided.", nameof(id));
        }

        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("Invoice id must be provided.", nameof(invoiceId));
        }

        if (originalAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Line item amount must be positive.");
        }

        if (decimal.Round(originalAmount, 2) != originalAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Line item amount must be expressed to the penny.");
        }

        Id = id;
        InvoiceId = invoiceId;
        Description = string.IsNullOrWhiteSpace(description) ? "Invoice line item" : description.Trim();
        OriginalAmount = originalAmount;
        DueDate = dueDate;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public Invoice Invoice { get; private set; } = null!;

    public string Description { get; private set; } = string.Empty;

    public decimal OriginalAmount { get; private set; }

    public DateOnly DueDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
