using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("LEDGER_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=AllocatedPaymentSys;Username=postgres;Password=Admin@123";

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new LedgerDbContext(options);
    }
}
