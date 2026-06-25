using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.Domain.Entities;

public class Subscription
{
    public string ConnectionId { get; init; } = default!;
    public string Symbol { get; init; } = default!;
    public ExchangeInfo BuyExchange { get; init; } = default!;
    public ExchangeInfo SellExchange { get; init; } = default!;
    public DateTime SubscribedAt { get; init; }
    public DateTime LastPingAt { get; set; }
}
