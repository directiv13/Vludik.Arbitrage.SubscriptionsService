using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Application.Common;
using SubscriptionsService.Application.UseCases.Subscribe;
using SubscriptionsService.Application.UseCases.Unsubscribe;
using SubscriptionsService.API.Messages;
using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.API.WebSockets;

/// <summary>
/// Manages the lifecycle of a single WebSocket connection: receive loop, message routing,
/// and cleanup on disconnect.
/// </summary>
public class WebSocketHandler
{
    private const int BufferSize = 8 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConnectionManager _connectionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebSocketHandler> _logger;

    public WebSocketHandler(
        ConnectionManager connectionManager,
        IServiceScopeFactory scopeFactory,
        ILogger<WebSocketHandler> logger)
    {
        _connectionManager = connectionManager;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, WebSocket socket, CancellationToken ct)
    {
        _connectionManager.Register(connectionId, socket);
        var hasSubscription = false;

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var message = await ReceiveMessageAsync(socket, ct);
                if (message is null)
                {
                    break; // close frame received or socket closed
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                hasSubscription = await RouteAsync(connectionId, message, hasSubscription, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — fall through to cleanup.
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WebSocket error on connection {ConnectionId}", connectionId);
        }
        finally
        {
            await CleanupAsync(connectionId, hasSubscription);
            _connectionManager.Unregister(connectionId);
        }
    }

    private async Task<bool> RouteAsync(string connectionId, string message, bool hasSubscription, CancellationToken ct)
    {
        BaseMessage? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<BaseMessage>(message, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Discarding malformed message on {ConnectionId}", connectionId);
            return hasSubscription;
        }

        switch (envelope?.Action?.ToLowerInvariant())
        {
            case "subscribe":
                var subscribe = JsonSerializer.Deserialize<SubscribeMessage>(message, JsonOptions);
                if (subscribe?.Params is not null)
                {
                    await InvokeAsync(s => s.SubscribeAsync(connectionId, ToSubscribeCommand(subscribe.Params), ct));
                    return true;
                }
                return hasSubscription;

            case "unsubscribe":
                var unsubscribe = JsonSerializer.Deserialize<UnsubscribeMessage>(message, JsonOptions);
                if (unsubscribe?.Params is not null)
                {
                    await InvokeAsync(s => s.UnsubscribeAsync(connectionId, ToUnsubscribeCommand(unsubscribe.Params), ct));
                }
                return false;

            case "ping":
                await InvokeAsync(s => s.RefreshPingAsync(connectionId, ct));
                return hasSubscription;

            default:
                _logger.LogDebug("Unknown action '{Action}' on {ConnectionId}", envelope?.Action, connectionId);
                return hasSubscription;
        }
    }

    private async Task CleanupAsync(string connectionId, bool hasSubscription)
    {
        if (!hasSubscription)
        {
            return;
        }

        try
        {
            // The unsubscribe use case resolves the live subscription from the store; the command
            // payload is only a placeholder here.
            await InvokeAsync(s => s.UnsubscribeAsync(
                connectionId,
                new UnsubscribeCommand(string.Empty, new ExchangeInfo(string.Empty, default), new ExchangeInfo(string.Empty, default)),
                CancellationToken.None));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup failed for connection {ConnectionId}", connectionId);
        }
    }

    private async Task InvokeAsync(Func<ISubscriptionService, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        await action(service);
    }

    private static async Task<string?> ReceiveMessageAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[BufferSize];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SubscribeCommand ToSubscribeCommand(SubscriptionParams p) =>
        new(p.Symbol, ToExchangeInfo(p.BuyExchange), ToExchangeInfo(p.SellExchange));

    private static UnsubscribeCommand ToUnsubscribeCommand(SubscriptionParams p) =>
        new(p.Symbol, ToExchangeInfo(p.BuyExchange), ToExchangeInfo(p.SellExchange));

    private static ExchangeInfo ToExchangeInfo(ExchangeDto dto) =>
        new(dto.Name, ContractTypeExtensions.ParseContractType(dto.ContractType));
}
