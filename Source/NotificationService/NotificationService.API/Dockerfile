# Build context is the repository root: the build needs Directory.*.props and the Shared project.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is cached until a dependency actually changes.
COPY Directory.Build.props Directory.Packages.props nuget.config ./
COPY Source/Shared/Shared.csproj Source/Shared/
COPY Source/NotificationService/NotificationService.API/NotificationService.API.csproj Source/NotificationService/NotificationService.API/
COPY Source/NotificationService/NotificationService.BLL/NotificationService.BLL.csproj Source/NotificationService/NotificationService.BLL/
RUN dotnet restore Source/NotificationService/NotificationService.API/NotificationService.API.csproj

COPY Source/Shared/ Source/Shared/
COPY Source/NotificationService/ Source/NotificationService/
RUN dotnet publish Source/NotificationService/NotificationService.API/NotificationService.API.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "NotificationService.API.dll"]
