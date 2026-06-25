using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.Application.Common;

/// <summary>
/// Builds Redis Pub/Sub topic keys: tick:{exchange}:{symbol}:{contractType}.
/// </summary>
public static class TopicBuilder
{
    public static string Build(string exchange, string symbol, string contractType) =>
        $"tick:{exchange}:{symbol}:{contractType}";

    public static string Build(string symbol, ExchangeInfo exchange) =>
        Build(exchange.Name, symbol, exchange.ContractType.ToTopicString());
}
