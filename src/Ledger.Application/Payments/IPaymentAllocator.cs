namespace Ledger.Application.Payments;

public interface IPaymentAllocator
{
    Task<AllocationResult> AllocatePaymentAsync(
        Guid invoiceId,
        decimal paymentAmount,
        CancellationToken cancellationToken = default);
}
