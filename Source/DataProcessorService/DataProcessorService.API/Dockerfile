# Build context is the repository root: the build needs Directory.*.props and the Shared project.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is cached until a dependency actually changes.
COPY Directory.Build.props Directory.Packages.props nuget.config .editorconfig stylecop.json ./
COPY Source/Shared/Shared.csproj Source/Shared/
COPY Source/DataProcessorService/DataProcessorService.API/DataProcessorService.API.csproj Source/DataProcessorService/DataProcessorService.API/
COPY Source/DataProcessorService/DataProcessorService.BLL/DataProcessorService.BLL.csproj Source/DataProcessorService/DataProcessorService.BLL/
COPY Source/DataProcessorService/DataProcessorService.DAL/DataProcessorService.DAL.csproj Source/DataProcessorService/DataProcessorService.DAL/
RUN dotnet restore Source/DataProcessorService/DataProcessorService.API/DataProcessorService.API.csproj

COPY Source/Shared/ Source/Shared/
COPY Source/DataProcessorService/ Source/DataProcessorService/
RUN dotnet publish Source/DataProcessorService/DataProcessorService.API/DataProcessorService.API.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DataProcessorService.API.dll"]
