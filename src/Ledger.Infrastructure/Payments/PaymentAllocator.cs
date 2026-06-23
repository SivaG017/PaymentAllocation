using System.Data;
using Ledger.Application.Payments;
using Ledger.Domain.Entities;
using Ledger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Infrastructure.Payments;

public sealed class PaymentAllocator(LedgerDbContext dbContext) : IPaymentAllocator
{
    public async Task<AllocationResult> AllocatePaymentAsync(
        Guid invoiceId,
        decimal paymentAmount,
        CancellationToken cancellationToken = default)
    {
        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("Invoice id must be provided.", nameof(invoiceId));
        }

        if (paymentAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentAmount), "Payment amount must be positive.");
        }

        if (decimal.Round(paymentAmount, 2) != paymentAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentAmount), "Payment amount must be expressed to the penny.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // All allocators lock the invoice first. This serializes allocations per invoice while preserving concurrency between invoices.
        var invoice = await dbContext.Invoices
            .FromSqlInterpolated($"SELECT * FROM invoices WHERE id = {invoiceId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (invoice is null)
        {
            throw new InvalidOperationException($"Invoice '{invoiceId}' was not found.");
        }

        var lineItems = await dbContext.InvoiceLineItems
            .Where(lineItem => lineItem.InvoiceId == invoiceId)
            .OrderBy(lineItem => lineItem.DueDate)
            .ThenBy(lineItem => lineItem.CreatedAt)
            .ThenBy(lineItem => lineItem.Id)
            .Select(lineItem => new OrderedLineItem(lineItem.Id))
            .ToListAsync(cancellationToken);

        var lineBalances = await dbContext.LedgerEntries
            .Where(entry => entry.InvoiceId == invoiceId && entry.InvoiceLineItemId != null)
            .GroupBy(entry => entry.InvoiceLineItemId!.Value)
            .Select(group => new LineBalance(group.Key, group.Sum(entry => entry.Amount)))
            .ToDictionaryAsync(balance => balance.InvoiceLineItemId, balance => balance.OutstandingBalance, cancellationToken);

        var outstandingBefore = await dbContext.LedgerEntries
            .Where(entry => entry.InvoiceId == invoiceId)
            .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0m;

        var remainingPayment = paymentAmount;
        var paymentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var allocations = new List<LineAllocation>();

        foreach (var lineItem in lineItems)
        {
            if (remainingPayment == 0)
            {
                break;
            }

            var lineOutstanding = lineBalances.GetValueOrDefault(lineItem.Id);
            if (lineOutstanding <= 0)
            {
                continue;
            }

            var amountToApply = Math.Min(lineOutstanding, remainingPayment);
            dbContext.LedgerEntries.Add(LedgerEntry.PaymentAllocation(invoiceId, lineItem.Id, amountToApply, now, paymentId));

            remainingPayment -= amountToApply;
            allocations.Add(new LineAllocation
            {
                InvoiceLineItemId = lineItem.Id,
                AmountApplied = amountToApply,
                OutstandingAfterAllocation = lineOutstanding - amountToApply
            });
        }

        if (remainingPayment > 0)
        {
            dbContext.LedgerEntries.Add(LedgerEntry.CustomerCredit(invoiceId, remainingPayment, now, paymentId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AllocationResult
        {
            OutstandingBalance = outstandingBefore - paymentAmount,
            Allocations = allocations
        };
    }

    private sealed record OrderedLineItem(Guid Id);

    private sealed record LineBalance(Guid InvoiceLineItemId, decimal OutstandingBalance);
}
