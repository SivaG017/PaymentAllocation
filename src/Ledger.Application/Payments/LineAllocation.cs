namespace Ledger.Application.Payments;

public sealed class LineAllocation
{
    public Guid InvoiceLineItemId { get; init; }

    public decimal AmountApplied { get; init; }

    public decimal OutstandingAfterAllocation { get; init; }
}
