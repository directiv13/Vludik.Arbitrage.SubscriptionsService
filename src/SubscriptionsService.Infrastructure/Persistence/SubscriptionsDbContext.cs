using Microsoft.EntityFrameworkCore;
using SubscriptionsService.Infrastructure.Persistence.Configurations;
using SubscriptionsService.Infrastructure.Persistence.Entities;

namespace SubscriptionsService.Infrastructure.Persistence;

public class SubscriptionsDbContext : DbContext
{
    public SubscriptionsDbContext(DbContextOptions<SubscriptionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<SubscriptionRecord> Subscriptions => Set<SubscriptionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SubscriptionRecordConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
