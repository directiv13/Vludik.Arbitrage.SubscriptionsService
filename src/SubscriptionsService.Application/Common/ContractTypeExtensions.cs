using Vludik.Arbitrage.Events.Entities;

namespace SubscriptionsService.Application.Common;

public static class ContractTypeExtensions
{
    /// <summary>
    /// Topic / persisted representation of a contract type, e.g. "spot" / "perpetual".
    /// </summary>
    public static string ToTopicString(this ContractType contractType) =>
        contractType.ToString().ToLowerInvariant();

    public static ContractType ParseContractType(string value) =>
        Enum.Parse<ContractType>(value, ignoreCase: true);
}
