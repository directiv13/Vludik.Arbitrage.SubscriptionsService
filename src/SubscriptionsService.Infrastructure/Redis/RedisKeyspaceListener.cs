using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SubscriptionsService.Application.Abstractions;

namespace SubscriptionsService.Infrastructure.Redis;

/// <summary>
/// Watches Redis key expiry via keyspace notifications. When a sub:{connectionId} key expires
/// (the client stopped pinging) it triggers the expiry use case — an implicit unsubscribe.
/// </summary>
public class RedisKeyspaceListener : IHostedService
{
    private const string SubPrefix = "sub:";
    private const string MetaPrefix = "sub-meta:";
    private const int DatabaseIndex = 0;
    private static readonly RedisChannel ExpiredChannel =
        RedisChannel.Literal($"__keyevent@{DatabaseIndex}__:expired");

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RedisKeyspaceListener> _logger;

    public RedisKeyspaceListener(
        IConnectionMultiplexer multiplexer,
        IServiceScopeFactory scopeFactory,
        ILogger<RedisKeyspaceListener> logger)
    {
        _multiplexer = multiplexer;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Ensure Redis emits expired-key events. Best-effort: a managed Redis may forbid CONFIG,
        // in which case the operator must enable notify-keyspace-events at the server level.
        foreach (var endpoint in _multiplexer.GetEndPoints())
        {
            var server = _multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
                continue;
            }

            try
            {
                await server.ConfigSetAsync("notify-keyspace-events", "Ex");
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex,
                    "Could not set notify-keyspace-events via CONFIG. Ensure the Redis server has " +
                    "keyspace expiry notifications ('Ex') enabled, otherwise TTL expiry cleanup will not fire.");
            }
        }

        var subscriber = _multiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(ExpiredChannel, OnKeyExpired);

        _logger.LogInformation("Redis keyspace expiry listener started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var subscriber = _multiplexer.GetSubscriber();
        await subscriber.UnsubscribeAsync(ExpiredChannel);
    }

    private void OnKeyExpired(RedisChannel channel, RedisValue value)
    {
        var key = value.ToString();
        if (string.IsNullOrEmpty(key) || key.StartsWith(MetaPrefix, StringComparison.Ordinal) ||
            !key.StartsWith(SubPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var connectionId = key[SubPrefix.Length..];
        _ = HandleExpiredAsync(connectionId);
    }

    private async Task HandleExpiredAsync(string connectionId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
            await service.HandleExpiredAsync(connectionId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle expiry for connection {ConnectionId}", connectionId);
        }
    }
}
