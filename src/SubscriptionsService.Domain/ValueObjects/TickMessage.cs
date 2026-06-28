namespace SubscriptionsService.Domain.ValueObjects;

public record TickMessage(
    string Exchange,
    string Symbol,
    string ContractType,
    decimal BestBid,
    decimal BestAsk,
    long Timestamp
);
