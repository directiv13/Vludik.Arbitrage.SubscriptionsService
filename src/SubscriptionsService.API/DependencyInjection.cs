using SubscriptionsService.Application.Abstractions;
using SubscriptionsService.API.WebSockets;

namespace SubscriptionsService.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddSingleton<ConnectionManager>();
        services.AddSingleton<IConnectionManager>(sp => sp.GetRequiredService<ConnectionManager>());
        services.AddSingleton<WebSocketHandler>();

        return services;
    }
}
