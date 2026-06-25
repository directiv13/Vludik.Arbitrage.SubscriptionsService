namespace SubscriptionsService.Application.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken ct) where T : class;
}
