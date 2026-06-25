using StackExchange.Redis;
using SubscriptionsService.Application.Abstractions;

namespace SubscriptionsService.Infrastructure.Redis;

/// <summary>
/// Reverse index topic -> connection ids, stored as Redis Sets (key: topic-subs:{topic}).
/// </summary>
public class TopicRegistry : ITopicRegistry
{
    private const string KeyPrefix = "topic-subs:";

    private readonly IConnectionMultiplexer _multiplexer;

    public TopicRegistry(IConnectionMultiplexer multiplexer)
    {
        _multiplexer = multiplexer;
    }

    private IDatabase Db => _multiplexer.GetDatabase();

    private static RedisKey Key(string topic) => KeyPrefix + topic;

    public Task AddConnectionAsync(string topic, string connectionId, CancellationToken ct) =>
        Db.SetAddAsync(Key(topic), connectionId);

    public Task RemoveConnectionAsync(string topic, string connectionId, CancellationToken ct) =>
        Db.SetRemoveAsync(Key(topic), connectionId);

    public async Task<IEnumerable<string>> GetConnectionsAsync(string topic, CancellationToken ct)
    {
        var members = await Db.SetMembersAsync(Key(topic));
        return members.Select(m => m.ToString());
    }
}
