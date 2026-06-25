using MassTransit;
using SubscriptionsService.Application.Abstractions;

namespace SubscriptionsService.Infrastructure.Messaging;

/// <summary>
/// Publish-only event publisher backed by MassTransit / RabbitMQ.
/// </summary>
public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<T>(T @event, CancellationToken ct) where T : class =>
        _publishEndpoint.Publish(@event, ct);
}
