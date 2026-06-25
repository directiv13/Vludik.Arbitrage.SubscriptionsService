using SubscriptionsService.Domain.Entities;

namespace SubscriptionsService.Application.Abstractions;

/// <summary>
/// SQL audit log of subscription lifecycle events.
/// </summary>
public interface ISubscriptionRepository
{
    Task CreateAsync(Subscription subscription, Guid id, CancellationToken ct);
    Task CloseAsync(Guid id, string status, DateTime closedAt, CancellationToken ct);

    /// <summary>
    /// Returns the id of the currently active row for <paramref name="connectionId"/>, or null.
    /// Lets close/expire flows recover the audit-row id when only the connection id is known.
    /// </summary>
    Task<Guid?> GetActiveIdAsync(string connectionId, CancellationToken ct);
}
