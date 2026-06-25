using Microsoft.EntityFrameworkCore;
using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Application.Common;
using SubscriptionsService.Domain.Entities;
using SubscriptionsService.Infrastructure.Persistence.Entities;

namespace SubscriptionsService.Infrastructure.Persistence;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly SubscriptionsDbContext _dbContext;

    public SubscriptionRepository(SubscriptionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(Subscription subscription, Guid id, CancellationToken ct)
    {
        var record = new SubscriptionRecord
        {
            Id = id,
            ConnectionId = subscription.ConnectionId,
            Symbol = subscription.Symbol,
            BuyExchange = subscription.BuyExchange.Name,
            BuyContract = subscription.BuyExchange.ContractType.ToTopicString(),
            SellExchange = subscription.SellExchange.Name,
            SellContract = subscription.SellExchange.ContractType.ToTopicString(),
            Status = "active",
            CreatedAt = subscription.SubscribedAt,
            ClosedAt = null
        };

        _dbContext.Subscriptions.Add(record);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task CloseAsync(Guid id, string status, DateTime closedAt, CancellationToken ct)
    {
        // Only an 'active' row can be closed — makes close idempotent and prevents a racing
        // close (e.g. disconnect cleanup vs. expiry) from overwriting an already-terminal status.
        var record = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == "active", ct);
        if (record is null)
        {
            return;
        }

        record.Status = status;
        record.ClosedAt = closedAt;
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<Guid?> GetActiveIdAsync(string connectionId, CancellationToken ct)
    {
        var record = await _dbContext.Subscriptions
            .Where(x => x.ConnectionId == connectionId && x.Status == "active")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return record?.Id;
    }
}
