using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Domain.Entities;
using SubscriptionsService.Infrastructure.Settings;

namespace SubscriptionsService.Infrastructure.Redis;

/// <summary>
/// Redis session state. The primary key (sub:{id}) carries the TTL; a non-expiring shadow key
/// (sub-meta:{id}) preserves the value so the keyspace-expiry handler can still recover it after
/// the primary key is gone.
/// </summary>
public class SubscriptionStore : ISubscriptionStore
{
    private const string KeyPrefix = "sub:";
    private const string MetaPrefix = "sub-meta:";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly TimeSpan _ttl;

    public SubscriptionStore(IConnectionMultiplexer multiplexer, IOptions<RedisSettings> settings)
    {
        _multiplexer = multiplexer;
        _ttl = TimeSpan.FromSeconds(settings.Value.SubscriptionTtlSeconds);
    }

    private IDatabase Db => _multiplexer.GetDatabase();

    private static RedisKey PrimaryKey(string connectionId) => KeyPrefix + connectionId;
    private static RedisKey MetaKey(string connectionId) => MetaPrefix + connectionId;

    public async Task SetAsync(string connectionId, Subscription subscription, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(subscription, JsonOptions);
        await Db.StringSetAsync(PrimaryKey(connectionId), json, _ttl);
        await Db.StringSetAsync(MetaKey(connectionId), json); // no TTL
    }

    public async Task<Subscription?> GetAsync(string connectionId, CancellationToken ct)
    {
        var value = await Db.StringGetAsync(PrimaryKey(connectionId));
        if (value.IsNullOrEmpty)
        {
            // Primary expired — fall back to the shadow copy.
            value = await Db.StringGetAsync(MetaKey(connectionId));
        }

        return value.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<Subscription>(value!, JsonOptions);
    }

    public async Task DeleteAsync(string connectionId, CancellationToken ct)
    {
        await Db.KeyDeleteAsync(new[] { PrimaryKey(connectionId), MetaKey(connectionId) });
    }

    public async Task RefreshTtlAsync(string connectionId, CancellationToken ct)
    {
        await Db.KeyExpireAsync(PrimaryKey(connectionId), _ttl);
    }

    public async Task<IEnumerable<string>> GetAllConnectionIdsAsync(CancellationToken ct)
    {
        var endpoints = _multiplexer.GetEndPoints();
        var ids = new List<string>();

        foreach (var endpoint in endpoints)
        {
            var server = _multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
                continue;
            }

            await foreach (var key in server.KeysAsync(pattern: KeyPrefix + "*").WithCancellation(ct))
            {
                ids.Add(key.ToString()[KeyPrefix.Length..]);
            }
        }

        return ids;
    }
}
