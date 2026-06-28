using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Application.Common;
using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.Infrastructure.Redis;

/// <summary>
/// Redis Pub/Sub listener with fan-out. Maintains a single Redis subscription per topic and
/// forwards each incoming tick to all registered per-connection handlers.
/// </summary>
public class TickSubscriber : ITickSubscriber
{
    private readonly ISubscriber _subscriber;
    private readonly ILogger<TickSubscriber> _logger;
    private readonly ConcurrentDictionary<string, TopicSubscription> _topics = new();

    public TickSubscriber(IConnectionMultiplexer multiplexer, ILogger<TickSubscriber> logger)
    {
        _subscriber = multiplexer.GetSubscriber();
        _logger = logger;
    }

    public async Task SubscribeAsync(string topic, string connectionId, Func<TickMessage, Task> handler, CancellationToken ct)
    {
        var subscription = _topics.GetOrAdd(topic, t => new TopicSubscription(t, _logger));
        subscription.AddHandler(connectionId, handler);
        await subscription.EnsureSubscribedAsync(_subscriber);
    }

    public async Task UnsubscribeAsync(string topic, string connectionId, CancellationToken ct)
    {
        if (!_topics.TryGetValue(topic, out var subscription))
        {
            return;
        }

        var isEmpty = subscription.RemoveHandler(connectionId);
        if (isEmpty && _topics.TryRemove(topic, out var removed))
        {
            await removed.UnsubscribeAsync(_subscriber);
        }
    }

    private sealed class TopicSubscription
    {
        private readonly string _topic;
        private readonly ILogger<TickSubscriber> _logger;
        private readonly ConcurrentDictionary<string, Func<TickMessage, Task>> _handlers = new();
        private readonly SemaphoreSlim _gate = new(1, 1);
        private bool _subscribed;

        public TopicSubscription(string topic, ILogger<TickSubscriber> logger)
        {
            _topic = topic;
            _logger = logger;
        }

        public void AddHandler(string connectionId, Func<TickMessage, Task> handler) =>
            _handlers[connectionId] = handler;

        /// <summary>Removes a handler; returns true when no handlers remain.</summary>
        public bool RemoveHandler(string connectionId)
        {
            _handlers.TryRemove(connectionId, out _);
            return _handlers.IsEmpty;
        }

        public async Task EnsureSubscribedAsync(ISubscriber subscriber)
        {
            if (_subscribed)
            {
                return;
            }

            await _gate.WaitAsync();
            try
            {
                if (_subscribed)
                {
                    return;
                }

                await subscriber.SubscribeAsync(RedisChannel.Literal(_topic), OnMessage);
                _subscribed = true;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task UnsubscribeAsync(ISubscriber subscriber)
        {
            await _gate.WaitAsync();
            try
            {
                if (!_subscribed)
                {
                    return;
                }

                await subscriber.UnsubscribeAsync(RedisChannel.Literal(_topic));
                _subscribed = false;
            }
            finally
            {
                _gate.Release();
            }
        }

        private void OnMessage(RedisChannel channel, RedisValue value)
        {
            if (value.IsNullOrEmpty)
            {
                return;
            }

            TickMessage? tick;
            try
            {
                tick = JsonSerializer.Deserialize<TickMessage>(value!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dropping malformed tick on {Topic}: {Payload}", _topic, value.ToString());
                return;
            }

            if (tick is null)
            {
                _logger.LogWarning("Dropping unparseable tick on {Topic}: {Payload}", _topic, value.ToString());
                return;
            }

            // Fan out. No buffering: forwarders fire immediately; failures are dropped.
            foreach (var handler in _handlers.Values)
            {
                _ = ForwardSafelyAsync(handler, tick);
            }
        }

        private static async Task ForwardSafelyAsync(Func<TickMessage, Task> handler, TickMessage tick)
        {
            try
            {
                await handler(tick);
            }
            catch
            {
                // Drop on send failure (rule: no buffering).
            }
        }
    }
}
