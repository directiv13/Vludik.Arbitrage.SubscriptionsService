using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.Application.Abstractions;

/// <summary>
/// Redis Pub/Sub listener with fan-out: a single Redis subscription per topic forwards ticks
/// to N per-connection handlers.
/// </summary>
public interface ITickSubscriber
{
    /// <summary>
    /// Registers <paramref name="handler"/> as a per-connection forwarder for <paramref name="topic"/>.
    /// The first connection on a topic opens the underlying Redis subscription.
    /// </summary>
    Task SubscribeAsync(string topic, string connectionId, Func<TickMessage, Task> handler, CancellationToken ct);

    /// <summary>
    /// Removes the forwarder for <paramref name="connectionId"/> on <paramref name="topic"/>.
    /// The last connection removed closes the underlying Redis subscription.
    /// </summary>
    Task UnsubscribeAsync(string topic, string connectionId, CancellationToken ct);
}
