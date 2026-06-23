using Ledger.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        EnforceAppendOnlyLedger();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceAppendOnlyLedger();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceAppendOnlyLedger()
    {
        var illegalLedgerChanges = ChangeTracker
            .Entries<LedgerEntry>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (illegalLedgerChanges.Count > 0)
        {
            throw new InvalidOperationException("Ledger entries are append-only and cannot be modified or deleted.");
        }
    }
}
