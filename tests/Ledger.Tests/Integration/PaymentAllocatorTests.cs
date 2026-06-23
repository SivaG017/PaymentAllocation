using Ledger.Domain.Entities;
using Ledger.Domain.Enums;
using Ledger.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Tests.Integration;

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

[Collection(nameof(PostgresCollection))]
public sealed class PaymentAllocatorTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Partial_payment_allocates_to_oldest_line_and_leaves_balance()
    {
        var lineIds = await SeedInvoiceAsync([
            new SeedLine(100m, new DateOnly(2026, 1, 10), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero)),
            new SeedLine(50m, new DateOnly(2026, 1, 20), new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))
        ]);

        await using var dbContext = fixture.CreateDbContext();
        var sut = new PaymentAllocator(dbContext);

        var result = await sut.AllocatePaymentAsync(lineIds.InvoiceId, 70m);

        Assert.Equal(80m, result.OutstandingBalance);
        var allocation = Assert.Single(result.Allocations);
        Assert.Equal(lineIds.LineItemIds[0], allocation.InvoiceLineItemId);
        Assert.Equal(70m, allocation.AmountApplied);
        Assert.Equal(30m, allocation.OutstandingAfterAllocation);
    }

    [Fact]
    public async Task Exact_payment_fully_settles_all_lines()
    {
        var lineIds = await SeedInvoiceAsync([
            new SeedLine(100m, new DateOnly(2026, 1, 10), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero)),
            new SeedLine(50m, new DateOnly(2026, 1, 20), new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))
        ]);

        await using var dbContext = fixture.CreateDbContext();
        var sut = new PaymentAllocator(dbContext);

        var result = await sut.AllocatePaymentAsync(lineIds.InvoiceId, 150m);

        Assert.Equal(0m, result.OutstandingBalance);
        Assert.Equal(2, result.Allocations.Count);
        Assert.All(result.Allocations, allocation => Assert.Equal(0m, allocation.OutstandingAfterAllocation));
    }

    [Fact]
    public async Task Overpayment_records_customer_credit_and_returns_negative_balance()
    {
        var lineIds = await SeedInvoiceAsync([
            new SeedLine(100m, new DateOnly(2026, 1, 10), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero))
        ]);

        await using var dbContext = fixture.CreateDbContext();
        var sut = new PaymentAllocator(dbContext);

        var result = await sut.AllocatePaymentAsync(lineIds.InvoiceId, 125m);

        Assert.Equal(-25m, result.OutstandingBalance);

        var credit = await dbContext.LedgerEntries.SingleAsync(entry =>
            entry.InvoiceId == lineIds.InvoiceId && entry.EntryType == LedgerEntryType.CustomerCredit);
        Assert.Null(credit.InvoiceLineItemId);
        Assert.Equal(-25m, credit.Amount);
    }

    [Fact]
    public async Task Oldest_first_uses_due_date_then_creation_order()
    {
        var lineIds = await SeedInvoiceAsync([
            new SeedLine(40m, new DateOnly(2026, 1, 20), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero)),
            new SeedLine(30m, new DateOnly(2026, 1, 10), new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero)),
            new SeedLine(20m, new DateOnly(2026, 1, 10), new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))
        ]);

        await using var dbContext = fixture.CreateDbContext();
        var sut = new PaymentAllocator(dbContext);

        var result = await sut.AllocatePaymentAsync(lineIds.InvoiceId, 45m);

        Assert.Collection(
            result.Allocations,
            allocation =>
            {
                Assert.Equal(lineIds.LineItemIds[2], allocation.InvoiceLineItemId);
                Assert.Equal(20m, allocation.AmountApplied);
            },
            allocation =>
            {
                Assert.Equal(lineIds.LineItemIds[1], allocation.InvoiceLineItemId);
                Assert.Equal(25m, allocation.AmountApplied);
            });
    }

    [Fact]
    public async Task Outstanding_balance_is_derived_from_ledger_entries()
    {
        var lineIds = await SeedInvoiceAsync([
            new SeedLine(100m, new DateOnly(2026, 1, 10), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero)),
            new SeedLine(25m, new DateOnly(2026, 1, 20), new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))
        ]);

        await using (var allocationContext = fixture.CreateDbContext())
        {
            var sut = new PaymentAllocator(allocationContext);
            await sut.AllocatePaymentAsync(lineIds.InvoiceId, 80m);
        }

        await using var dbContext = fixture.CreateDbContext();
        var balanceFromLedger = await dbContext.LedgerEntries
            .Where(entry => entry.InvoiceId == lineIds.InvoiceId)
            .SumAsync(entry => entry.Amount);

        Assert.Equal(45m, balanceFromLedger);
    }

    [Fact]
    public async Task Concurrent_allocations_are_serialized_per_invoice()
    {
        var lineIds = await SeedInvoiceAsync([
            new SeedLine(100m, new DateOnly(2026, 1, 10), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero))
        ]);

        var firstTask = AllocateWithNewContextAsync(lineIds.InvoiceId, 80m);
        var secondTask = AllocateWithNewContextAsync(lineIds.InvoiceId, 80m);

        var results = await Task.WhenAll(firstTask, secondTask);

        await using var dbContext = fixture.CreateDbContext();
        var balanceFromLedger = await dbContext.LedgerEntries
            .Where(entry => entry.InvoiceId == lineIds.InvoiceId)
            .SumAsync(entry => entry.Amount);

        Assert.Equal(-60m, balanceFromLedger);
        Assert.Equal(-60m, results.Min(result => result.OutstandingBalance));
        Assert.Equal(1, await dbContext.LedgerEntries.CountAsync(entry =>
            entry.InvoiceId == lineIds.InvoiceId && entry.EntryType == LedgerEntryType.CustomerCredit));
    }

    private async Task<Ledger.Application.Payments.AllocationResult> AllocateWithNewContextAsync(Guid invoiceId, decimal amount)
    {
        await using var dbContext = fixture.CreateDbContext();
        var sut = new PaymentAllocator(dbContext);
        return await sut.AllocatePaymentAsync(invoiceId, amount);
    }

    private async Task<SeedInvoiceResult> SeedInvoiceAsync(IReadOnlyList<SeedLine> lines)
    {
        await using var dbContext = fixture.CreateDbContext();

        var invoice = new Invoice(Guid.NewGuid(), $"CUST-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var lineItemIds = new List<Guid>();

        foreach (var line in lines)
        {
            var lineItem = invoice.AddLineItem(Guid.NewGuid(), "Funding line", line.Amount, line.DueDate, line.CreatedAt);
            lineItemIds.Add(lineItem.Id);
        }

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        foreach (var lineItem in invoice.LineItems)
        {
            dbContext.LedgerEntries.Add(LedgerEntry.Charge(invoice.Id, lineItem.Id, lineItem.OriginalAmount, invoice.CreatedAt));
        }

        await dbContext.SaveChangesAsync();

        return new SeedInvoiceResult(invoice.Id, lineItemIds);
    }

    private sealed record SeedLine(decimal Amount, DateOnly DueDate, DateTimeOffset CreatedAt);

    private sealed record SeedInvoiceResult(Guid InvoiceId, IReadOnlyList<Guid> LineItemIds);
}
