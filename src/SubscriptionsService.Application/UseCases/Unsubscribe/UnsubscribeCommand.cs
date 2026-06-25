using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.Application.UseCases.Unsubscribe;

public record UnsubscribeCommand(
    string Symbol,
    ExchangeInfo BuyExchange,
    ExchangeInfo SellExchange
);
