using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Application.Common;
using Vludik.Arbitrage.Shared.Models;
using Vludik.Arbitrage.SubscriptionsService.Shared.Events;

namespace SubscriptionsService.Application.UseCases.HandleExpired;

/// <summary>
/// Triggered by a Redis keyspace expiry notification for sub:{connectionId}.
/// The primary key is already gone by the time we run, so the store recovers the subscription
/// from the non-expiring shadow key.
/// </summary>
public class HandleExpiredHandler
{
    private readonly ISubscriptionStore _store;
    private readonly ITopicRegistry _topicRegistry;
    private readonly ITickSubscriber _tickSubscriber;
    private readonly IConnectionManager _connectionManager;
    private readonly ISubscriptionRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public HandleExpiredHandler(
        ISubscriptionStore store,
        ITopicRegistry topicRegistry,
        ITickSubscriber tickSubscriber,
        IConnectionManager connectionManager,
        ISubscriptionRepository repository,
        IEventPublisher eventPublisher)
    {
        _store = store;
        _topicRegistry = topicRegistry;
        _tickSubscriber = tickSubscriber;
        _connectionManager = connectionManager;
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task HandleAsync(string connectionId, HandleExpiredCommand command, CancellationToken ct)
    {
        // 1. Recover the subscription (from the shadow key) before it is fully gone.
        var subscription = await _store.GetAsync(connectionId, ct);
        if (subscription is null)
        {
            return;
        }

        var buyTopic = TopicBuilder.Build(subscription.Symbol, subscription.BuyExchange);
        var sellTopic = TopicBuilder.Build(subscription.Symbol, subscription.SellExchange);

        // 2. Stop forwarding both topics for this connection.
        await _tickSubscriber.UnsubscribeAsync(buyTopic, connectionId, ct);
        await _tickSubscriber.UnsubscribeAsync(sellTopic, connectionId, ct);

        // 3. Drop the connection from the topic reverse index.
        await _topicRegistry.RemoveConnectionAsync(buyTopic, connectionId, ct);
        await _topicRegistry.RemoveConnectionAsync(sellTopic, connectionId, ct);

        // 4. Remove the leftover shadow key BEFORE closing the socket. Closing the socket wakes
        //    the connection's receive loop, whose disconnect-cleanup also calls unsubscribe; once
        //    the keys are gone that cleanup becomes a no-op, avoiding a duplicate event.
        await _store.DeleteAsync(connectionId, ct);

        // 5. Close the SQL audit row (idempotent: only an 'active' row is closed).
        var closedAt = DateTime.UtcNow;
        var id = await _repository.GetActiveIdAsync(connectionId, ct);
        if (id is not null)
        {
            await _repository.CloseAsync(id.Value, "expired", closedAt, ct);
        }

        // 6. Publish the lifecycle event.
        await _eventPublisher.PublishAsync(new SubscriptionDeletedEvent(
            id ?? Guid.Empty,
            connectionId,
            subscription.Symbol,
            new ExchangeRef(subscription.BuyExchange.Name, subscription.BuyExchange.ContractType),
            new ExchangeRef(subscription.SellExchange.Name, subscription.SellExchange.ContractType),
            "expired",
            new DateTimeOffset(closedAt).ToUnixTimeMilliseconds()), ct);

        // 7. Finally, close the WebSocket.
        await _connectionManager.CloseAsync(connectionId, ct);
    }
}
