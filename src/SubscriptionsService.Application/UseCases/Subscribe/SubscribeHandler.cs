using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Application.Common;
using SubscriptionsService.Application.UseCases.Unsubscribe;
using SubscriptionsService.Domain.Entities;
using SubscriptionsService.Domain.Events;
using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.Application.UseCases.Subscribe;

public class SubscribeHandler
{
    private readonly ISubscriptionStore _store;
    private readonly ITopicRegistry _topicRegistry;
    private readonly ITickSubscriber _tickSubscriber;
    private readonly ISubscriptionRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IConnectionManager _connectionManager;
    private readonly UnsubscribeHandler _unsubscribeHandler;

    public SubscribeHandler(
        ISubscriptionStore store,
        ITopicRegistry topicRegistry,
        ITickSubscriber tickSubscriber,
        ISubscriptionRepository repository,
        IEventPublisher eventPublisher,
        IConnectionManager connectionManager,
        UnsubscribeHandler unsubscribeHandler)
    {
        _store = store;
        _topicRegistry = topicRegistry;
        _tickSubscriber = tickSubscriber;
        _repository = repository;
        _eventPublisher = eventPublisher;
        _connectionManager = connectionManager;
        _unsubscribeHandler = unsubscribeHandler;
    }

    public async Task HandleAsync(string connectionId, SubscribeCommand command, CancellationToken ct)
    {
        // 1. One subscription per connection: tear the previous one down first.
        var existing = await _store.GetAsync(connectionId, ct);
        if (existing is not null)
        {
            await _unsubscribeHandler.HandleAsync(
                connectionId,
                new UnsubscribeCommand(existing.Symbol, existing.BuyExchange, existing.SellExchange),
                ct);
        }

        // 2. Build the domain entity.
        var now = DateTime.UtcNow;
        var subscription = new Subscription
        {
            ConnectionId = connectionId,
            Symbol = command.Symbol,
            BuyExchange = command.BuyExchange,
            SellExchange = command.SellExchange,
            SubscribedAt = now,
            LastPingAt = now
        };

        var id = Guid.NewGuid();
        var buyTopic = TopicBuilder.Build(command.Symbol, command.BuyExchange);
        var sellTopic = TopicBuilder.Build(command.Symbol, command.SellExchange);

        // 3. Persist Redis session state (TTL 35s + shadow key for expiry recovery).
        await _store.SetAsync(connectionId, subscription, ct);

        // 4. Register the connection in the topic reverse index for both topics.
        await _topicRegistry.AddConnectionAsync(buyTopic, connectionId, ct);
        await _topicRegistry.AddConnectionAsync(sellTopic, connectionId, ct);

        // 5. Begin forwarding both tick streams to this connection.
        var forwarder = CreateForwarder(connectionId);
        await _tickSubscriber.SubscribeAsync(buyTopic, connectionId, forwarder, ct);
        await _tickSubscriber.SubscribeAsync(sellTopic, connectionId, forwarder, ct);

        // 6. Write the SQL audit row.
        await _repository.CreateAsync(subscription, id, ct);

        // 7. Publish the lifecycle event.
        await _eventPublisher.PublishAsync(new SubscriptionCreatedEvent(
            id,
            connectionId,
            command.Symbol,
            new ExchangeRef(command.BuyExchange.Name, command.BuyExchange.ContractType.ToTopicString()),
            new ExchangeRef(command.SellExchange.Name, command.SellExchange.ContractType.ToTopicString()),
            new DateTimeOffset(now).ToUnixTimeMilliseconds()), ct);
    }

    private Func<TickMessage, Task> CreateForwarder(string connectionId) =>
        tick => _connectionManager.SendAsync(connectionId, TickSerializer.Serialize(tick), CancellationToken.None);
}
