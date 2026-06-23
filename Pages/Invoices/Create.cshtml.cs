using Ledger.Domain.Entities;
using Ledger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PaymentAllocation.Pages.Invoices;

public class CreateModel(LedgerDbContext dbContext) : PageModel
{
    [BindProperty]
    public CreateInvoiceInput Input { get; set; } = new();

    public void OnGet()
    {
        EnsureAtLeastOneLineItem();
    }

    public IActionResult OnPostAddLine()
    {
        EnsureAtLeastOneLineItem();
        Input.LineItems.Add(new CreateLineItemInput
        {
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7))
        });

        return Page();
    }

    public IActionResult OnPostRemoveLine(int index)
    {
        EnsureAtLeastOneLineItem();

        if (index >= 0 && index < Input.LineItems.Count)
        {
            Input.LineItems.RemoveAt(index);
        }

        if (Input.LineItems.Count == 0)
        {
            Input.LineItems.Add(new CreateLineItemInput
            {
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7))
            });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        EnsureAtLeastOneLineItem();
        Input.LineItems = Input.LineItems.Where(line => !line.IsBlank()).ToList();

        if (string.IsNullOrWhiteSpace(Input.CustomerReference))
        {
            ModelState.AddModelError(nameof(Input.CustomerReference), "Customer reference is required.");
        }

        if (Input.LineItems.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one line item.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        var customerReference = Input.CustomerReference!.Trim();
        var invoice = new Invoice(Guid.NewGuid(), customerReference, now);

        foreach (var line in Input.LineItems)
        {
            invoice.AddLineItem(
                Guid.NewGuid(),
                line.Description ?? "Funding line",
                line.Amount ?? 0m,
                line.DueDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date),
                now);
        }

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var lineItem in invoice.LineItems)
        {
            dbContext.LedgerEntries.Add(LedgerEntry.Charge(invoice.Id, lineItem.Id, lineItem.OriginalAmount, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["Message"] = $"Invoice {invoice.CustomerReference} created.";
        return RedirectToPage("/Invoices/Details", new { id = invoice.Id });
    }

    private void EnsureAtLeastOneLineItem()
    {
        if (Input.LineItems.Count == 0)
        {
            Input.LineItems.Add(new CreateLineItemInput
            {
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7))
            });
        }
    }

    public sealed class CreateInvoiceInput
    {
        public string? CustomerReference { get; set; }

        public List<CreateLineItemInput> LineItems { get; set; } = [];
    }

    public sealed class CreateLineItemInput
    {
        public string? Description { get; set; }

        public decimal? Amount { get; set; }

        public DateOnly? DueDate { get; set; }

        public bool IsBlank() => string.IsNullOrWhiteSpace(Description) && Amount is null && DueDate is null;
    }
}
