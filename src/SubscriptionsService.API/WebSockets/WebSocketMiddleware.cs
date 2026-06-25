namespace SubscriptionsService.API.WebSockets;

/// <summary>
/// Intercepts requests to /ws, upgrades them to WebSocket connections, and hands each one off
/// to a <see cref="WebSocketHandler"/>.
/// </summary>
public class WebSocketMiddleware
{
    private const string Path = "/ws";

    private readonly RequestDelegate _next;
    private readonly WebSocketHandler _handler;

    public WebSocketMiddleware(RequestDelegate next, WebSocketHandler handler)
    {
        _next = next;
        _handler = handler;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();

        await _handler.HandleAsync(connectionId, socket, context.RequestAborted);
    }
}
