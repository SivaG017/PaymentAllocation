using Ledger.Application.Payments;
using Ledger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace PaymentAllocation.Pages.Invoices;

public class DetailsModel(LedgerDbContext dbContext, IPaymentAllocator allocator) : PageModel
{
    [BindProperty]
    public decimal PaymentAmount { get; set; }

    public InvoiceViewModel? Invoice { get; private set; }

    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        await LoadInvoiceAsync(id, cancellationToken);
        Message = TempData["Message"] as string;

        return Invoice is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAllocateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (PaymentAmount <= 0)
        {
            ModelState.AddModelError(nameof(PaymentAmount), "Enter a payment amount greater than zero.");
        }

        await LoadInvoiceAsync(id, cancellationToken);
        if (Invoice is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await allocator.AllocatePaymentAsync(id, PaymentAmount, cancellationToken);
        TempData["Message"] = $"Payment of £{PaymentAmount:0.00} allocated. New balance: £{result.OutstandingBalance:0.00}.";
        return RedirectToPage(new { id });
    }

    private async Task LoadInvoiceAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
        {
            Invoice = null;
            return;
        }

        var lineItems = await dbContext.InvoiceLineItems
            .AsNoTracking()
            .Where(line => line.InvoiceId == id)
            .OrderBy(line => line.DueDate)
            .ThenBy(line => line.CreatedAt)
            .ThenBy(line => line.Id)
            .Select(line => new LineItemViewModel
            {
                Id = line.Id,
                Description = line.Description,
                OriginalAmount = line.OriginalAmount,
                DueDate = line.DueDate,
                CreatedAt = line.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var entries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.InvoiceId == id)
            .OrderBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.Id)
            .Select(entry => new LedgerEntryViewModel
            {
                Id = entry.Id,
                EntryType = entry.EntryType,
                Amount = entry.Amount,
                CreatedAt = entry.CreatedAt,
                LineItemId = entry.InvoiceLineItemId,
                PaymentId = entry.PaymentId
            })
            .ToListAsync(cancellationToken);

        var lineBalances = entries
            .Where(entry => entry.LineItemId is not null)
            .GroupBy(entry => entry.LineItemId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Amount));

        foreach (var line in lineItems)
        {
            line.OutstandingBalance = lineBalances.GetValueOrDefault(line.Id);
        }

        Invoice = new InvoiceViewModel
        {
            Id = invoice.Id,
            CustomerReference = invoice.CustomerReference,
            CreatedAt = invoice.CreatedAt,
            OutstandingBalance = entries.Sum(entry => entry.Amount),
            LineItems = lineItems,
            LedgerEntries = entries
        };
    }

    public sealed class InvoiceViewModel
    {
        public Guid Id { get; init; }

        public string CustomerReference { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public decimal OutstandingBalance { get; init; }

        public List<LineItemViewModel> LineItems { get; init; } = [];

        public List<LedgerEntryViewModel> LedgerEntries { get; init; } = [];
    }

    public sealed class LineItemViewModel
    {
        public Guid Id { get; init; }

        public string Description { get; init; } = string.Empty;

        public decimal OriginalAmount { get; init; }

        public DateOnly DueDate { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public decimal OutstandingBalance { get; set; }
    }

    public sealed class LedgerEntryViewModel
    {
        public Guid Id { get; init; }

        public Ledger.Domain.Enums.LedgerEntryType EntryType { get; init; }

        public decimal Amount { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public Guid? LineItemId { get; init; }

        public Guid? PaymentId { get; init; }
    }
}
