using SubscriptionsService.API;
using SubscriptionsService.API.WebSockets;
using SubscriptionsService.Application;
using SubscriptionsService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi();

var app = builder.Build();

app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();

app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();
