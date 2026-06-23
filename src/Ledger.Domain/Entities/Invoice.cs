namespace Ledger.Domain.Entities;

public sealed class Invoice
{
    private readonly List<InvoiceLineItem> _lineItems = [];

    private Invoice()
    {
    }

    public Invoice(Guid id, string customerReference, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Invoice id must be provided.", nameof(id));
        }

        Id = id;
        CustomerReference = string.IsNullOrWhiteSpace(customerReference)
            ? throw new ArgumentException("Customer reference is required.", nameof(customerReference))
            : customerReference.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string CustomerReference { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems;

    public InvoiceLineItem AddLineItem(
        Guid id,
        string description,
        decimal amount,
        DateOnly dueDate,
        DateTimeOffset createdAt)
    {
        var lineItem = new InvoiceLineItem(id, Id, description, amount, dueDate, createdAt);
        _lineItems.Add(lineItem);
        return lineItem;
    }
}
