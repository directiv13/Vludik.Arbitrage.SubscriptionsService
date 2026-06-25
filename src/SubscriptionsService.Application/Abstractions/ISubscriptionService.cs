using SubscriptionsService.Application.UseCases.Subscribe;
using SubscriptionsService.Application.UseCases.Unsubscribe;

namespace SubscriptionsService.Application.Abstractions;

/// <summary>
/// Facade used by the API and infrastructure layers — delegates to use case handlers.
/// </summary>
public interface ISubscriptionService
{
    Task SubscribeAsync(string connectionId, SubscribeCommand cmd, CancellationToken ct);
    Task UnsubscribeAsync(string connectionId, UnsubscribeCommand cmd, CancellationToken ct);
    Task RefreshPingAsync(string connectionId, CancellationToken ct);
    Task HandleExpiredAsync(string connectionId, CancellationToken ct);
}
