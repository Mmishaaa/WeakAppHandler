# Build context is the repository root: the build needs Directory.*.props and the Shared project.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is cached until a dependency actually changes.
COPY Directory.Build.props Directory.Packages.props nuget.config .editorconfig stylecop.json ./
COPY Source/Shared/Shared.csproj Source/Shared/
COPY Source/GraphQlGateway/GraphQlGateway.API/GraphQlGateway.API.csproj Source/GraphQlGateway/GraphQlGateway.API/
COPY Source/GraphQlGateway/GraphQlGateway.BLL/GraphQlGateway.BLL.csproj Source/GraphQlGateway/GraphQlGateway.BLL/
COPY Source/GraphQlGateway/GraphQlGateway.DAL/GraphQlGateway.DAL.csproj Source/GraphQlGateway/GraphQlGateway.DAL/
RUN dotnet restore Source/GraphQlGateway/GraphQlGateway.API/GraphQlGateway.API.csproj

COPY Source/Shared/ Source/Shared/
COPY Source/GraphQlGateway/ Source/GraphQlGateway/
RUN dotnet publish Source/GraphQlGateway/GraphQlGateway.API/GraphQlGateway.API.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "GraphQlGateway.API.dll"]
