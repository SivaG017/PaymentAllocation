using Ledger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace PaymentAllocation.Pages;

public class IndexModel(LedgerDbContext dbContext) : PageModel
{
    public IReadOnlyList<InvoiceSummary> Invoices { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Invoices = await dbContext.Invoices
            .AsNoTracking()
            .OrderByDescending(invoice => invoice.CreatedAt)
            .Select(invoice => new InvoiceSummary
            {
                Id = invoice.Id,
                CustomerReference = invoice.CustomerReference,
                CreatedAt = invoice.CreatedAt,
                OutstandingBalance = dbContext.LedgerEntries
                    .Where(entry => entry.InvoiceId == invoice.Id)
                    .Sum(entry => (decimal?)entry.Amount) ?? 0m,
                LineCount = dbContext.InvoiceLineItems.Count(line => line.InvoiceId == invoice.Id)
            })
            .ToListAsync(cancellationToken);
    }

    public sealed record InvoiceSummary
    {
        public Guid Id { get; init; }

        public string CustomerReference { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public decimal OutstandingBalance { get; init; }

        public int LineCount { get; init; }
    }
}
