using SubscriptionsService.Application.Abstractions;

namespace SubscriptionsService.Application.UseCases.RefreshPing;

public class RefreshPingHandler
{
    private readonly ISubscriptionStore _store;

    public RefreshPingHandler(ISubscriptionStore store)
    {
        _store = store;
    }

    public async Task HandleAsync(string connectionId, RefreshPingCommand command, CancellationToken ct)
    {
        // No active subscription -> nothing to refresh.
        var subscription = await _store.GetAsync(connectionId, ct);
        if (subscription is null)
        {
            return;
        }

        // Update LastPingAt and reset the 35s TTL by rewriting the value.
        subscription.LastPingAt = DateTime.UtcNow;
        await _store.SetAsync(connectionId, subscription, ct);
    }
}
