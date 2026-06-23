using Ledger.Application.Payments;
using Ledger.Infrastructure.Payments;
using Ledger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLedgerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Ledger")
            ?? Environment.GetEnvironmentVariable("LEDGER_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=AllocatedPaymentSys;Username=postgres;Password=Admin@123";

        services.AddDbContext<LedgerDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPaymentAllocator, PaymentAllocator>();

        return services;
    }
}
