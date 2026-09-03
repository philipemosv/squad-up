# syntax=docker/dockerfile:1@sha256:ecfaec9ed6d810b56388c508f4121597bfbba70d41a6dfeee4d8cad5f295fc32

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.400-noble@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build

WORKDIR /source

COPY --link Directory.Build.props Directory.Packages.props ./
COPY --link src/BuildingBlocks/SquadUp.ServiceDefaults/SquadUp.ServiceDefaults.csproj src/BuildingBlocks/SquadUp.ServiceDefaults/packages.lock.json ./src/BuildingBlocks/SquadUp.ServiceDefaults/
COPY --link src/Api/SquadUp.Identity.Domain/SquadUp.Identity.Domain.csproj src/Api/SquadUp.Identity.Domain/packages.lock.json ./src/Api/SquadUp.Identity.Domain/
COPY --link src/Api/SquadUp.Identity.Application/SquadUp.Identity.Application.csproj src/Api/SquadUp.Identity.Application/packages.lock.json ./src/Api/SquadUp.Identity.Application/
COPY --link src/Api/SquadUp.Identity.Infrastructure/SquadUp.Identity.Infrastructure.csproj src/Api/SquadUp.Identity.Infrastructure/packages.lock.json ./src/Api/SquadUp.Identity.Infrastructure/
COPY --link src/Api/SquadUp.Profile.Domain/SquadUp.Profile.Domain.csproj src/Api/SquadUp.Profile.Domain/packages.lock.json ./src/Api/SquadUp.Profile.Domain/
COPY --link src/Api/SquadUp.Profile.Application/SquadUp.Profile.Application.csproj src/Api/SquadUp.Profile.Application/packages.lock.json ./src/Api/SquadUp.Profile.Application/
COPY --link src/Api/SquadUp.Profile.Infrastructure/SquadUp.Profile.Infrastructure.csproj src/Api/SquadUp.Profile.Infrastructure/packages.lock.json ./src/Api/SquadUp.Profile.Infrastructure/
COPY --link src/Api/SquadUp.Api/SquadUp.Api.csproj src/Api/SquadUp.Api/packages.lock.json ./src/Api/SquadUp.Api/

RUN dotnet restore src/Api/SquadUp.Api/SquadUp.Api.csproj \
    --locked-mode

COPY --link src/BuildingBlocks/SquadUp.ServiceDefaults/ ./src/BuildingBlocks/SquadUp.ServiceDefaults/
COPY --link src/Api/ ./src/Api/

RUN dotnet publish src/Api/SquadUp.Api/SquadUp.Api.csproj \
    --configuration Release \
    --no-restore \
    --property:UseAppHost=false \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-noble-chiseled@sha256:0839314d08bb65da369135389a5d8291f75ace587fbb0488f469eb92c62eef68 AS final

WORKDIR /app
COPY --link --from=build /app ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "SquadUp.Api.dll"]
