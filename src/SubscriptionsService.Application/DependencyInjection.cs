using Microsoft.Extensions.DependencyInjection;
using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.Application.Services;
using SubscriptionsService.Application.UseCases.HandleExpired;
using SubscriptionsService.Application.UseCases.RefreshPing;
using SubscriptionsService.Application.UseCases.Subscribe;
using SubscriptionsService.Application.UseCases.Unsubscribe;

namespace SubscriptionsService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SubscribeHandler>();
        services.AddScoped<UnsubscribeHandler>();
        services.AddScoped<RefreshPingHandler>();
        services.AddScoped<HandleExpiredHandler>();

        services.AddScoped<ISubscriptionService, SubscriptionService>();

        return services;
    }
}
