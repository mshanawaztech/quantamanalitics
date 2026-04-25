# syntax=docker/dockerfile:1.7

# ---------- Build ----------
# SDK image is large but only used for compilation; the runtime image is slim.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first to maximize Docker layer caching: when only source
# changes, restore stays cached.
COPY Directory.Build.props ./
COPY QuantamAnalytics.Domain/QuantamAnalytics.Domain.csproj QuantamAnalytics.Domain/
COPY QuantamAnalytics.Infrastructure/QuantamAnalytics.Infrastructure.csproj QuantamAnalytics.Infrastructure/
COPY QuantamAnalytics.Api/QuantamAnalytics.Api.csproj QuantamAnalytics.Api/

RUN dotnet restore QuantamAnalytics.Api/QuantamAnalytics.Api.csproj

# Now copy the rest and publish.
COPY QuantamAnalytics.Domain/ QuantamAnalytics.Domain/
COPY QuantamAnalytics.Infrastructure/ QuantamAnalytics.Infrastructure/
COPY QuantamAnalytics.Api/ QuantamAnalytics.Api/

RUN dotnet publish QuantamAnalytics.Api/QuantamAnalytics.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---------- Runtime ----------
# `aspnet:10.0` already contains a non-root `app` user — use it.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Container Apps probes hit /health and /ready on this port. Must match
# infra/modules/container-app.bicep targetPort.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

USER app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "QuantamAnalytics.Api.dll"]
