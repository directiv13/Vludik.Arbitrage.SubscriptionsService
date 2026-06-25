using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using StackExchange.Redis;
using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Infrastructure.Messaging;
using SubscriptionsService.Infrastructure.Persistence;
using SubscriptionsService.Infrastructure.Redis;
using SubscriptionsService.Infrastructure.Settings;
using System.Text.Json.Serialization;
using Vludik.Arbitrage.Events;

namespace SubscriptionsService.Infrastructure;

public static class DependencyInjection
{
    // Shared topic exchange the arbitrage platform publishes domain events to;
    // the Market Data service binds one queue per event type to this exchange by routing key.
    private const string EventsExchange = "arbitrage.events";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var redisSettings = configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>() ?? new RedisSettings();
        var rabbitSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>() ?? new RabbitMqSettings();

        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));

        // Redis. Admin mode is required for the keyspace listener to run CONFIG SET
        // (notify-keyspace-events) at startup.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisSettings.ConnectionString);
            options.AllowAdmin = true;
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<ISubscriptionStore, SubscriptionStore>();
        services.AddSingleton<ITopicRegistry, TopicRegistry>();
        services.AddSingleton<ITickSubscriber, TickSubscriber>();
        services.AddHostedService<RedisKeyspaceListener>();

        // SQL persistence
        services.AddDbContext<SubscriptionsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("SubscriptionsDb")));
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

        // Messaging (publish-only)
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((_, cfg) =>
            {
                cfg.Host(rabbitSettings.Host, h =>
                {
                    h.Username(rabbitSettings.Username);
                    h.Password(rabbitSettings.Password);
                });

                cfg.ConfigureJsonSerializerOptions(opts =>
                {
                    opts.Converters.Add(new JsonStringEnumConverter());
                    return opts;
                });

                ConfigureTopicPublish<SubscriptionCreatedEvent>(cfg, "subscription.created");
                ConfigureTopicPublish<SubscriptionDeletedEvent>(cfg, "subscription.deleted");
            });
        });

        return services;
    }

    private static void ConfigureTopicPublish<T>(IRabbitMqBusFactoryConfigurator cfg, string routingKey)
        where T : class
    {
        cfg.Message<T>(m => m.SetEntityName(EventsExchange));
        cfg.Publish<T>(p => p.ExchangeType = ExchangeType.Topic);
        cfg.Send<T>(s => s.UseRoutingKeyFormatter(_ => routingKey));
    }
}
