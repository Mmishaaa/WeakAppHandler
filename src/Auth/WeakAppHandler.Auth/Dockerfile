# syntax=docker/dockerfile:1
#
# Build context is the repository root - build with:
#   docker build -f src/Auth/WeakAppHandler.Auth/Dockerfile -t weakapphandler-auth .
#
# The whole src/ tree is copied in one layer rather than csproj-by-csproj: every host transitively
# references SharedKernel/Contracts/ServiceDefaults (and Processor's hosts additionally reference
# their own Domain/Application/Infrastructure layers), so a per-project restore layer needs to stay
# in lockstep with each project's actual reference graph as it evolves - a single COPY that always
# matches whatever the graph currently is trades a little restore-layer caching for not silently
# missing a project reference when it changes.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props stylecop.json .editorconfig ./
COPY src/ src/

RUN dotnet publish src/Auth/WeakAppHandler.Auth/WeakAppHandler.Auth.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# $APP_UID is the non-root "app" user (uid 64198) baked into every official .NET 8+ runtime image,
# Alpine and Debian alike - no need to create a custom user/group ourselves. This app never writes to
# its own disk (Serilog goes to the console; EF migrations run against PostgreSQL at startup -
# TASK-047, so a fresh `docker compose up` needs no manual `dotnet ef database update` step - which
# writes to the database, not the container's filesystem), so it can still run with
# `docker run --read-only --security-opt=no-new-privileges` unmodified; those are runtime flags set
# where the container is started (TASK-047's compose wiring), not something a Dockerfile can declare.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENTRYPOINT ["dotnet", "WeakAppHandler.Auth.dll"]
