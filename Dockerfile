FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY src/SubscriptionsService.Domain/SubscriptionsService.Domain.csproj src/SubscriptionsService.Domain/
COPY src/SubscriptionsService.Application/SubscriptionsService.Application.csproj src/SubscriptionsService.Application/
COPY src/SubscriptionsService.Infrastructure/SubscriptionsService.Infrastructure.csproj src/SubscriptionsService.Infrastructure/
COPY src/SubscriptionsService.API/SubscriptionsService.API.csproj src/SubscriptionsService.API/
RUN dotnet restore src/SubscriptionsService.API/SubscriptionsService.API.csproj

COPY src/ src/
WORKDIR /src/src/SubscriptionsService.API
RUN dotnet build SubscriptionsService.API.csproj -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish SubscriptionsService.API.csproj -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# appsettings.json hardcodes "Urls": "http://localhost:8080" for local dev; ASPNETCORE_URLS
# can't override it (the no-prefix env var provider keeps the "ASPNETCORE_" prefix, so it
# never collides with the "Urls" key from appsettings.json). Override the literal key instead,
# otherwise Kestrel binds to loopback-only and the container is unreachable from outside.
ENV Urls=http://+:8080
ENTRYPOINT ["dotnet", "SubscriptionsService.API.dll"]
