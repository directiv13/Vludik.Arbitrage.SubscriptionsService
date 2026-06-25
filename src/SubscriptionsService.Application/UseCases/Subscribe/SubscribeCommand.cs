using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.Application.UseCases.Subscribe;

public record SubscribeCommand(
    string Symbol,
    ExchangeInfo BuyExchange,
    ExchangeInfo SellExchange
);
