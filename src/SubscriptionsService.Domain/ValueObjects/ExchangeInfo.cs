using Vludik.Arbitrage.Events.Entities;

namespace SubscriptionsService.Domain.ValueObjects;

public record ExchangeInfo (string Name, ContractType ContractType);
