# syntax=docker/dockerfile:1@sha256:ecfaec9ed6d810b56388c508f4121597bfbba70d41a6dfeee4d8cad5f295fc32

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.400-noble@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build

WORKDIR /source

# Restore from project metadata first so source-only changes reuse this layer.
COPY --link Directory.Build.props Directory.Packages.props ./
COPY --link src/BuildingBlocks/SquadUp.ServiceDefaults/SquadUp.ServiceDefaults.csproj src/BuildingBlocks/SquadUp.ServiceDefaults/packages.lock.json ./src/BuildingBlocks/SquadUp.ServiceDefaults/
COPY --link src/Lobby/SquadUp.LobbyService.Domain/SquadUp.LobbyService.Domain.csproj src/Lobby/SquadUp.LobbyService.Domain/packages.lock.json ./src/Lobby/SquadUp.LobbyService.Domain/
COPY --link src/Lobby/SquadUp.LobbyService.Application/SquadUp.LobbyService.Application.csproj src/Lobby/SquadUp.LobbyService.Application/packages.lock.json ./src/Lobby/SquadUp.LobbyService.Application/
COPY --link src/Lobby/SquadUp.LobbyService.Infrastructure/SquadUp.LobbyService.Infrastructure.csproj src/Lobby/SquadUp.LobbyService.Infrastructure/packages.lock.json ./src/Lobby/SquadUp.LobbyService.Infrastructure/
COPY --link src/Lobby/SquadUp.LobbyService.Api/SquadUp.LobbyService.Api.csproj src/Lobby/SquadUp.LobbyService.Api/packages.lock.json ./src/Lobby/SquadUp.LobbyService.Api/

RUN dotnet restore src/Lobby/SquadUp.LobbyService.Api/SquadUp.LobbyService.Api.csproj \
    --locked-mode

COPY --link src/BuildingBlocks/SquadUp.ServiceDefaults/ ./src/BuildingBlocks/SquadUp.ServiceDefaults/
COPY --link src/Lobby/ ./src/Lobby/

RUN dotnet publish src/Lobby/SquadUp.LobbyService.Api/SquadUp.LobbyService.Api.csproj \
    --configuration Release \
    --no-restore \
    --property:UseAppHost=false \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-noble-chiseled@sha256:0839314d08bb65da369135389a5d8291f75ace587fbb0488f469eb92c62eef68 AS final

WORKDIR /app
COPY --link --from=build /app ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "SquadUp.LobbyService.Api.dll"]
