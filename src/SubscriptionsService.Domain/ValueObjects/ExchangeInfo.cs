using Vludik.Arbitrage.Shared.Enums;

namespace SubscriptionsService.Domain.ValueObjects;

public record ExchangeInfo (string Name, ContractType ContractType);
