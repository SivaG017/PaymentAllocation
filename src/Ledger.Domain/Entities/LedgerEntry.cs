using Ledger.Domain.Enums;

namespace Ledger.Domain.Entities;

public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    private LedgerEntry(
        Guid id,
        Guid invoiceId,
        Guid? invoiceLineItemId,
        LedgerEntryType entryType,
        decimal amount,
        DateTimeOffset createdAt,
        Guid? paymentId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Ledger entry id must be provided.", nameof(id));
        }

        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("Invoice id must be provided.", nameof(invoiceId));
        }

        if (amount == 0 || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Ledger amount must be non-zero and expressed to the penny.");
        }

        Id = id;
        InvoiceId = invoiceId;
        InvoiceLineItemId = invoiceLineItemId;
        EntryType = entryType;
        Amount = amount;
        CreatedAt = createdAt;
        PaymentId = paymentId;
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public Invoice Invoice { get; private set; } = null!;

    public Guid? InvoiceLineItemId { get; private set; }

    public InvoiceLineItem? InvoiceLineItem { get; private set; }

    public LedgerEntryType EntryType { get; private set; }

    public decimal Amount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? PaymentId { get; private set; }

    public static LedgerEntry Charge(Guid invoiceId, Guid invoiceLineItemId, decimal amount, DateTimeOffset createdAt)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Charge amount must be positive.");
        }

        return new LedgerEntry(Guid.NewGuid(), invoiceId, invoiceLineItemId, LedgerEntryType.Charge, amount, createdAt, null);
    }

    public static LedgerEntry PaymentAllocation(
        Guid invoiceId,
        Guid invoiceLineItemId,
        decimal amount,
        DateTimeOffset createdAt,
        Guid paymentId)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment allocation amount must be positive.");
        }

        return new LedgerEntry(
            Guid.NewGuid(),
            invoiceId,
            invoiceLineItemId,
            LedgerEntryType.PaymentAllocation,
            -amount,
            createdAt,
            paymentId);
    }

    public static LedgerEntry CustomerCredit(Guid invoiceId, decimal amount, DateTimeOffset createdAt, Guid paymentId)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Customer credit amount must be positive.");
        }

        return new LedgerEntry(
            Guid.NewGuid(),
            invoiceId,
            null,
            LedgerEntryType.CustomerCredit,
            -amount,
            createdAt,
            paymentId);
    }
}
