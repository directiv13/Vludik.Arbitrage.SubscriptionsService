using SubscriptionsService.Domain.Entities;

namespace SubscriptionsService.Application.Abstractions;

/// <summary>
/// Redis session state for active subscriptions (key: sub:{connectionId}, TTL 35s).
/// </summary>
public interface ISubscriptionStore
{
    Task SetAsync(string connectionId, Subscription subscription, CancellationToken ct);
    Task<Subscription?> GetAsync(string connectionId, CancellationToken ct);
    Task DeleteAsync(string connectionId, CancellationToken ct);
    Task RefreshTtlAsync(string connectionId, CancellationToken ct);
    Task<IEnumerable<string>> GetAllConnectionIdsAsync(CancellationToken ct);
}
