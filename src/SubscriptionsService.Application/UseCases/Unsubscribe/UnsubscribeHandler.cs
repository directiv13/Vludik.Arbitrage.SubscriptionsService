using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Application.Common;
using SubscriptionsService.Domain.Events;

namespace SubscriptionsService.Application.UseCases.Unsubscribe;

public class UnsubscribeHandler
{
    private readonly ISubscriptionStore _store;
    private readonly ITopicRegistry _topicRegistry;
    private readonly ITickSubscriber _tickSubscriber;
    private readonly ISubscriptionRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public UnsubscribeHandler(
        ISubscriptionStore store,
        ITopicRegistry topicRegistry,
        ITickSubscriber tickSubscriber,
        ISubscriptionRepository repository,
        IEventPublisher eventPublisher)
    {
        _store = store;
        _topicRegistry = topicRegistry;
        _tickSubscriber = tickSubscriber;
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task HandleAsync(string connectionId, UnsubscribeCommand command, CancellationToken ct)
    {
        // 1. Load current state; if there is nothing to remove, no-op.
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

        // 4. Remove the Redis session state (primary + shadow key).
        await _store.DeleteAsync(connectionId, ct);

        // 5. Close the SQL audit row.
        var closedAt = DateTime.UtcNow;
        var id = await _repository.GetActiveIdAsync(connectionId, ct);
        if (id is not null)
        {
            await _repository.CloseAsync(id.Value, "deleted", closedAt, ct);
        }

        // 6. Publish the lifecycle event.
        await _eventPublisher.PublishAsync(new SubscriptionDeletedEvent(
            id ?? Guid.Empty,
            connectionId,
            subscription.Symbol,
            new ExchangeRef(subscription.BuyExchange.Name, subscription.BuyExchange.ContractType.ToTopicString()),
            new ExchangeRef(subscription.SellExchange.Name, subscription.SellExchange.ContractType.ToTopicString()),
            "unsubscribed",
            new DateTimeOffset(closedAt).ToUnixTimeMilliseconds()), ct);
    }
}
