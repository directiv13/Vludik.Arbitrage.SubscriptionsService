namespace SubscriptionsService.Application.Abstractions;

/// <summary>
/// Reverse index of topic -> active connection ids, backed by Redis Sets
/// (key: topic-subs:{topic}).
/// </summary>
public interface ITopicRegistry
{
    Task AddConnectionAsync(string topic, string connectionId, CancellationToken ct);
    Task RemoveConnectionAsync(string topic, string connectionId, CancellationToken ct);
    Task<IEnumerable<string>> GetConnectionsAsync(string topic, CancellationToken ct);
}
