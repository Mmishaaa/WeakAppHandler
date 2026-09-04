# syntax=docker/dockerfile:1
#
# Build context is the repository root - build with:
#   docker build -f src/Ingestor/WeakAppHandler.Ingestor/Dockerfile -t weakapphandler-ingestor .
#
# See src/Auth/WeakAppHandler.Auth/Dockerfile for why the whole src/ tree is copied in one layer
# instead of csproj-by-csproj.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props stylecop.json ./
COPY src/ src/

RUN dotnet publish src/Ingestor/WeakAppHandler.Ingestor/WeakAppHandler.Ingestor.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# $APP_UID is the non-root "app" user (uid 64198) baked into every official .NET 8+ runtime image,
# Alpine and Debian alike - no need to create a custom user/group ourselves. The polling loop never
# writes to disk (Serilog goes to the console), so it can run with
# `docker run --read-only --security-opt=no-new-privileges` unmodified.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENTRYPOINT ["dotnet", "WeakAppHandler.Ingestor.dll"]
