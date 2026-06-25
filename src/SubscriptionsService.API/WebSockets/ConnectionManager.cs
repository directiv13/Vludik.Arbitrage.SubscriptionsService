using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using SubscriptionsService.Application.Abstractions;

namespace SubscriptionsService.API.WebSockets;

/// <summary>
/// Thread-safe registry of open WebSocket connections. Sends to a single socket are serialized
/// (a WebSocket does not allow concurrent send operations).
/// </summary>
public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, ConnectionContext> _connections = new();

    public void Register(string connectionId, WebSocket socket) =>
        _connections[connectionId] = new ConnectionContext(socket);

    public void Unregister(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var context))
        {
            context.SendGate.Dispose();
        }
    }

    public async Task SendAsync(string connectionId, string payload, CancellationToken ct)
    {
        if (!_connections.TryGetValue(connectionId, out var context) ||
            context.Socket.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(payload);

        await context.SendGate.WaitAsync(ct);
        try
        {
            if (context.Socket.State == WebSocketState.Open)
            {
                await context.Socket.SendAsync(
                    new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, ct);
            }
        }
        finally
        {
            context.SendGate.Release();
        }
    }

    public async Task CloseAsync(string connectionId, CancellationToken ct)
    {
        if (!_connections.TryGetValue(connectionId, out var context))
        {
            return;
        }

        try
        {
            if (context.Socket.State == WebSocketState.Open)
            {
                await context.Socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Subscription closed", ct);
            }
        }
        catch
        {
            // Socket may already be torn down — ignore.
        }
        finally
        {
            Unregister(connectionId);
        }
    }

    private sealed class ConnectionContext
    {
        public ConnectionContext(WebSocket socket) => Socket = socket;

        public WebSocket Socket { get; }
        public SemaphoreSlim SendGate { get; } = new(1, 1);
    }
}
