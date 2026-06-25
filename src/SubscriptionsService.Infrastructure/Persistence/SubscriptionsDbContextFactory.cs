using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SubscriptionsService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations) so they do not need to boot the
/// full application host. The runtime uses the connection string from configuration instead.
/// </summary>
public class SubscriptionsDbContextFactory : IDesignTimeDbContextFactory<SubscriptionsDbContext>
{
    public SubscriptionsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SubscriptionsDbContext>()
            .UseNpgsql("Host=localhost;Database=arbitrage;Username=postgres;Password=postgres")
            .Options;

        return new SubscriptionsDbContext(options);
    }
}
