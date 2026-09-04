# syntax=docker/dockerfile:1
#
# Build context is the repository root - build with:
#   docker build -f src/Notification/WeakAppHandler.Notification.Api/Dockerfile -t weakapphandler-notification .
#
# See src/Auth/WeakAppHandler.Auth/Dockerfile for why the whole src/ tree is copied in one layer
# instead of csproj-by-csproj.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props stylecop.json ./
COPY src/ src/

RUN dotnet publish src/Notification/WeakAppHandler.Notification.Api/WeakAppHandler.Notification.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# $APP_UID is the non-root "app" user (uid 64198) baked into every official .NET 8+ runtime image,
# Alpine and Debian alike - no need to create a custom user/group ourselves. No Program.cs in this
# solution calls Database.Migrate() at startup, so schema migrations are applied out-of-band via
# `dotnet ef database update`, not by this container.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENTRYPOINT ["dotnet", "WeakAppHandler.Notification.Api.dll"]
