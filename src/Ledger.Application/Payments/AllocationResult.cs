namespace Ledger.Application.Payments;

public sealed class AllocationResult
{
    public decimal OutstandingBalance { get; init; }

    public IReadOnlyCollection<LineAllocation> Allocations { get; init; } = [];
}
