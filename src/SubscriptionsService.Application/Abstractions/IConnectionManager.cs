namespace SubscriptionsService.Application.Abstractions;

/// <summary>
/// Lets use case handlers push data to / close individual WebSocket connections.
/// </summary>
public interface IConnectionManager
{
    Task SendAsync(string connectionId, string payload, CancellationToken ct);
    Task CloseAsync(string connectionId, CancellationToken ct);
}
