using SubscriptionsService.Domain.Enums;

namespace SubscriptionsService.Domain.ValueObjects;

public record ExchangeInfo(string Name, ContractType ContractType);
