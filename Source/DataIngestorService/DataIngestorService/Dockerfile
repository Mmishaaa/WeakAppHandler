# Build context is the repository root: the build needs Directory.*.props and the Shared project.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is cached until a dependency actually changes.
COPY Directory.Build.props Directory.Packages.props nuget.config .editorconfig stylecop.json ./
COPY Source/Shared/Shared.csproj Source/Shared/
COPY Source/DataIngestorService/DataIngestorService/DataIngestorService.csproj Source/DataIngestorService/DataIngestorService/
RUN dotnet restore Source/DataIngestorService/DataIngestorService/DataIngestorService.csproj

COPY Source/Shared/ Source/Shared/
COPY Source/DataIngestorService/ Source/DataIngestorService/
RUN dotnet publish Source/DataIngestorService/DataIngestorService/DataIngestorService.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DataIngestorService.dll"]
