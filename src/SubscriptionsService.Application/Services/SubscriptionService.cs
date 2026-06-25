using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Application.UseCases.HandleExpired;
using SubscriptionsService.Application.UseCases.RefreshPing;
using SubscriptionsService.Application.UseCases.Subscribe;
using SubscriptionsService.Application.UseCases.Unsubscribe;

namespace SubscriptionsService.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly SubscribeHandler _subscribeHandler;
    private readonly UnsubscribeHandler _unsubscribeHandler;
    private readonly RefreshPingHandler _refreshPingHandler;
    private readonly HandleExpiredHandler _handleExpiredHandler;

    public SubscriptionService(
        SubscribeHandler subscribeHandler,
        UnsubscribeHandler unsubscribeHandler,
        RefreshPingHandler refreshPingHandler,
        HandleExpiredHandler handleExpiredHandler)
    {
        _subscribeHandler = subscribeHandler;
        _unsubscribeHandler = unsubscribeHandler;
        _refreshPingHandler = refreshPingHandler;
        _handleExpiredHandler = handleExpiredHandler;
    }

    public Task SubscribeAsync(string connectionId, SubscribeCommand cmd, CancellationToken ct) =>
        _subscribeHandler.HandleAsync(connectionId, cmd, ct);

    public Task UnsubscribeAsync(string connectionId, UnsubscribeCommand cmd, CancellationToken ct) =>
        _unsubscribeHandler.HandleAsync(connectionId, cmd, ct);

    public Task RefreshPingAsync(string connectionId, CancellationToken ct) =>
        _refreshPingHandler.HandleAsync(connectionId, new RefreshPingCommand(), ct);

    public Task HandleExpiredAsync(string connectionId, CancellationToken ct) =>
        _handleExpiredHandler.HandleAsync(connectionId, new HandleExpiredCommand(), ct);
}
