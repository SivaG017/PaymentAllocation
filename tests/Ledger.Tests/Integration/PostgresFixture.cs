using Ledger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Ledger.Tests.Integration;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ledger_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public LedgerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new LedgerDbContext(options);
    }
}
