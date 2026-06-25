using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubscriptionsService.Infrastructure.Persistence.Entities;

namespace SubscriptionsService.Infrastructure.Persistence.Configurations;

public class SubscriptionRecordConfiguration : IEntityTypeConfiguration<SubscriptionRecord>
{
    public void Configure(EntityTypeBuilder<SubscriptionRecord> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ConnectionId).HasColumnName("connection_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Symbol).HasColumnName("symbol").HasMaxLength(20).IsRequired();
        builder.Property(x => x.BuyExchange).HasColumnName("buy_exchange").HasMaxLength(50).IsRequired();
        builder.Property(x => x.BuyContract).HasColumnName("buy_contract").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SellExchange).HasColumnName("sell_exchange").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SellContract).HasColumnName("sell_contract").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");

        // Supports GetActiveIdAsync(connectionId) lookups.
        builder.HasIndex(x => new { x.ConnectionId, x.Status });
    }
}
