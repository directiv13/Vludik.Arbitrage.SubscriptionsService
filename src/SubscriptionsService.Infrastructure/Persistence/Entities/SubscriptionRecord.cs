namespace SubscriptionsService.Infrastructure.Persistence.Entities;

/// <summary>
/// Flat EF Core entity for the append-only audit log. No domain types.
/// </summary>
public class SubscriptionRecord
{
    public Guid Id { get; set; }
    public string ConnectionId { get; set; } = default!;
    public string Symbol { get; set; } = default!;
    public string BuyExchange { get; set; } = default!;
    public string BuyContract { get; set; } = default!;
    public string SellExchange { get; set; } = default!;
    public string SellContract { get; set; } = default!;
    public string Status { get; set; } = default!; // "active" | "deleted" | "expired"
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
