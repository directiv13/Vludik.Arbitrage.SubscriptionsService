using Vludik.Arbitrage.Shared.Enums;

namespace SubscriptionsService.Application.Common;

public static class ContractTypeExtensions
{
    /// <summary>
    /// Redis Pub/Sub topic representation, e.g. "Spot" / "Perpetual". Channel names are matched
    /// exactly (case-sensitive) by Redis, so this must mirror the casing the Market Data service
    /// publishes with — it intentionally does NOT lowercase.
    /// </summary>
    public static string ToTopicString(this ContractType contractType) =>
        contractType.ToString();

    /// <summary>
    /// Persisted representation of a contract type for SQL audit rows, e.g. "spot" / "perpetual".
    /// </summary>
    public static string ToPersistedString(this ContractType contractType) =>
        contractType.ToString().ToLowerInvariant();

    public static ContractType ParseContractType(string value) =>
        Enum.Parse<ContractType>(value, ignoreCase: true);
}
