namespace SubscriptionsService.API.Messages;

public class SubscriptionParams
{
    public string Symbol { get; set; } = default!;
    public ExchangeDto BuyExchange { get; set; } = default!;
    public ExchangeDto SellExchange { get; set; } = default!;
}
