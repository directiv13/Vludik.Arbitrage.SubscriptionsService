namespace SubscriptionsService.Domain.Events;

public record SubscriptionDeletedEvent(
    Guid SubscriptionId,
    string ConnectionId,
    string Symbol,
    ExchangeRef BuyExchange,
    ExchangeRef SellExchange,
    string Reason, // "unsubscribed" | "expired"
    long Timestamp
);
