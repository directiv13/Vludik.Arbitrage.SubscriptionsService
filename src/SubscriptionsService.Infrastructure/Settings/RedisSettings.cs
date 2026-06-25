namespace SubscriptionsService.Infrastructure.Settings;

public class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public int SubscriptionTtlSeconds { get; set; } = 35;
}
